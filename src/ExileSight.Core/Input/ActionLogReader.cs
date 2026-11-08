using System.Text;
using System.Text.Json;
using ExileSight.Core.Status;
namespace ExileSight.Core.Input;

public sealed record ActionLogEntry(DateTimeOffset Timestamp, string? Hotkey, string Action,
    string Outcome, string Reason, string? Verb)
{
    public string Trigger => Hotkey is null ? "—" : TriggerNames.Display(Hotkey);
    public bool Sent => Outcome == "Success";
    public string Result => Sent ? "Sent" : string.IsNullOrEmpty(Reason) ? Outcome : Reason;
}
public sealed record ActionLogSnapshot(IReadOnlyList<ActionLogEntry> Entries, int Skipped, bool Truncated, string? Note = null, bool Missing = false);

/// <summary>Reads at most 1 MiB and the last 200 physical lines; never blocks the UI dispatcher.</summary>
public static class ActionLogReader
{
    private static ActionLogSnapshot MissingFile() => new([], 0, false,
        "actions.log does not exist yet. Open log folder opens its folder when available.", true);
    public const int MaximumBytes = 1024 * 1024;
    public static Task<ActionLogSnapshot> ReadAsync(string path) => Task.Run(() => Read(path));
    public static ActionLogSnapshot Read(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = file.Length;
            var start = Math.Max(0, length - MaximumBytes);
            file.Position = start;
            var bytes = new byte[(int)(length - start)];
            var count = 0;
            while (count < bytes.Length)
            {
                var read = file.Read(bytes, count, bytes.Length - count);
                if (read == 0) break;
                count += read;
            }
            var text = Encoding.UTF8.GetString(bytes, 0, count);
            if (start > 0)
            {
                var newline = text.IndexOf('\n');
                text = newline < 0 ? "" : text[(newline + 1)..];
            }
            var lines = text.Split('\n').AsEnumerable();
            if (text.EndsWith('\n')) lines = lines.SkipLast(1);
            var entries = new List<ActionLogEntry>();
            var skipped = 0;
            foreach (var line in lines.TakeLast(200).Reverse())
            {
                try
                {
                    using var json = JsonDocument.Parse(line);
                    var root = json.RootElement;
                    var timestamp = root.GetProperty("timestamp").GetDateTimeOffset();
                    string Required(string name) => root.GetProperty(name).GetString() ?? throw new JsonException();
                    string? Optional(string name) => root.TryGetProperty(name, out var value) ? value.GetString() : null;
                    entries.Add(new(timestamp, Optional("hotkey"), Required("action"), Required("outcome"),
                        Required("reason"), Optional("verb")));
                }
                catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
                { skipped++; }
            }
            return new(entries, skipped, start > 0);
        }
        catch (FileNotFoundException) { return MissingFile(); }
        catch (DirectoryNotFoundException) { return MissingFile(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { return new([], 0, false, "Could not read actions.log (" + error.GetType().Name + "). Try again after access is restored."); }
    }
}
