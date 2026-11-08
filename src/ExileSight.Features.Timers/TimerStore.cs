using System.Text.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ExileSight.Features.Timers.Tests")]

namespace ExileSight.Features.Timers;

/// <summary>Bounded local data, flushed before atomic replacement. Only invalid JSON is quarantined;
/// transient read failures and unknown data leave the original read-only for this session.</summary>
public sealed class TimerStore
{
    public const int MaxTimers = 50, MaxBytes = 128 * 1024;
    private readonly string path;
    private readonly object sync = new();
    private TimerState[] rows = [];
    private bool unavailable;
    public string? Notice { get; private set; }
    public TimerStore(string directory) : this(directory,
        path => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) { }
    internal TimerStore(string directory, Func<string, Stream> open)
    {
        path = Path.Combine(directory, "timers.json");
        try
        {
            using var file = open(path);
            if (file.Length > MaxBytes) { Block("Timers exceed the size limit; file kept read-only."); return; }
            using var document = JsonDocument.Parse(file);
            var loaded = TimerDocument.Read(document.RootElement);
            rows = loaded.Rows;
            if (loaded.ReadOnly) Block("Timers contain unknown fields or a newer schema; file kept read-only.");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Block("Timers could not be read; file kept, saving disabled for this session. Restart to retry."); }
        catch (JsonException)
        {
            var kept = path + "." + Guid.NewGuid().ToString("N") + ".corrupt";
            try { File.Move(path, kept); }
            catch (Exception moveError) when (moveError is IOException or UnauthorizedAccessException)
            { kept = path; unavailable = true; }
            Notice = "Timers could not be loaded. Previous data kept at " + kept;
        }
    }
    private void Block(string notice) { unavailable = true; Notice = notice; }
    public TimerState[] Snapshot() { lock (sync) return rows.ToArray(); }
    public void Save(TimerState[] values)
    {
        lock (sync)
        {
            if (unavailable) throw new IOException("Timers are read-only for this session.");
            if (!TimerDocument.Valid(values)) throw new ArgumentException("Invalid or excessive timers.");
            var next = values.ToArray();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new TimerDocument(1, next), TimerDocument.Options);
            if (bytes.Length > MaxBytes) throw new IOException("Timers exceed size limit.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            try
            {
                using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                { file.Write(bytes); file.Flush(true); }
                File.Move(temp, path, true);
                rows = next;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
