using System.Text.Json;

namespace ExileSight.Features.Timers;

/// <summary>Bounded local data, flushed before atomic replacement. Unrecoverable originals are never overwritten.</summary>
public sealed class TimerStore
{
    public const int MaxTimers = 50, MaxBytes = 128 * 1024;
    private readonly string path;
    private readonly object sync = new();
    private TimerState[] rows = [];
    private bool unavailable;
    public string? Notice { get; private set; }
    public TimerStore(string directory)
    {
        path = Path.Combine(directory, "timers.json");
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > MaxBytes) throw new JsonException("Timers exceed size limit.");
            var loaded = JsonSerializer.Deserialize<TimerState[]>(file);
            if (!Valid(loaded)) throw new JsonException("Invalid timers.");
            rows = loaded!;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            var kept = path + "." + Guid.NewGuid().ToString("N") + ".corrupt";
            try { File.Move(path, kept); }
            catch (Exception moveError) when (moveError is IOException or UnauthorizedAccessException)
            { kept = path; unavailable = true; }
            Notice = "Timers could not be loaded. Previous data kept at " + kept;
        }
    }
    public TimerState[] Snapshot() { lock (sync) return rows.ToArray(); }
    public void Save(TimerState[] values)
    {
        lock (sync)
        {
            if (unavailable) throw new IOException("Previous timers could not be preserved.");
            if (!Valid(values)) throw new ArgumentException("Invalid or excessive timers.");
            var next = values.ToArray();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(next);
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
    private static bool Valid(TimerState[]? values) => values is not null && values.Length <= MaxTimers &&
        values.All(r => r is not null && r.IsValid()) && values.Select(r => r.Id).Distinct().Count() == values.Length;
}
