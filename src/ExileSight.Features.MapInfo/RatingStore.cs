using System.Text.Json;

namespace ExileSight.Features.MapInfo;

/// <summary>Atomic bounded local ratings. Invalid or unfamiliar documents remain untouched and read-only.</summary>
public sealed class RatingStore
{
    public const int MaxEntries = 4096, MaxBytes = 128 * 1024;
    private readonly string path;
    private readonly object sync = new();
    private Dictionary<string, int> ratings = new(StringComparer.Ordinal);
    public string? Notice { get; private set; }
    private bool readOnly;
    public RatingStore(string directory)
    {
        path = Path.Combine(directory, "ratings.json");
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > MaxBytes) throw new JsonException();
            using var doc = JsonDocument.Parse(file);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
                !root.TryGetProperty("schema", out var schema) || !schema.TryGetInt32(out var version) || version != 1 ||
                !root.TryGetProperty("ratings", out var values) || values.ValueKind != JsonValueKind.Object) throw new JsonException();
            foreach (var p in values.EnumerateObject())
            {
                if (!ValidId(p.Name) || !p.Value.TryGetInt32(out var rank) || rank is < 0 or > 3 ||
                    !ratings.TryAdd(p.Name, rank) || ratings.Count > MaxEntries) throw new JsonException();
            }
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { ratings.Clear(); readOnly = true; Notice = "Ratings unavailable; original file preserved read-only. Restart after repairing it."; }
    }
    private static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128;
    public int Get(string id) { lock (sync) return ratings.GetValueOrDefault(id); }
    public int Cycle(string id)
    {
        lock (sync)
        {
            if (readOnly) throw new IOException(Notice);
            if (!ValidId(id)) throw new ArgumentException("Invalid mod ID.", nameof(id));
            var next = new Dictionary<string, int>(ratings) { [id] = (Get(id) + 1) % 4 };
            if (next.Count > MaxEntries) throw new IOException("Rating count limit reached.");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, ratings = next });
            if (bytes.Length > MaxBytes) throw new IOException("Rating size limit reached.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes); file.Flush(true); }
                File.Move(temp, path, true); ratings = next;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return next[id];
        }
    }
}
