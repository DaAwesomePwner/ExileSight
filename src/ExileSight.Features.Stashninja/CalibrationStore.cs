using System.Text.Json;

namespace ExileSight.Features.Stashninja;

public sealed class CalibrationStore
{
    private readonly string path;
    private readonly object sync = new();
    private Dictionary<string, Transform> values = [];
    private bool blocked;
    public string? Notice { get; private set; }
    public CalibrationStore(string directory)
    {
        path = Path.Combine(directory, "calibration.json");
        try
        {
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length > 1024 * 1024) throw new JsonException("Calibration too large");
            values = JsonSerializer.Deserialize<Dictionary<string, Transform>>(File.ReadAllText(path)) ?? throw new JsonException();
            if (values.Count > 1000 || values.Any(v => v.Value is null || !v.Value.IsValid)) throw new JsonException();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            values = [];
            try { File.Move(path, path + "." + Guid.NewGuid().ToString("N") + ".corrupt"); }
            catch (Exception move) when (move is IOException or UnauthorizedAccessException) { blocked = true; }
            Notice = "Invalid calibration preserved; recalibrate this resolution.";
        }
    }
    private static string Key(Layout l, Resolution r) => JsonSerializer.Serialize(new
    { l.TabId, l.Page, l.ReferenceResolution, l.Cells, l.FolderOffsets, Resolution = r });
    public Transform? Get(Layout layout, Resolution resolution)
    { lock (sync) return values.GetValueOrDefault(Key(layout, resolution)); }
    public void Save(Layout layout, Resolution resolution, Transform transform)
    {
        if (!transform.IsValid || resolution.Width <= 0 || resolution.Height <= 0) throw new ArgumentException("Invalid calibration");
        lock (sync)
        {
            if (blocked) throw new IOException("Cannot overwrite calibration that could not be preserved");
            var next = new Dictionary<string, Transform>(values) { [Key(layout, resolution)] = transform };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(next);
            if (next.Count > 1000 || bytes.Length > 1024 * 1024) throw new IOException("Calibration store is full");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes); file.Flush(true); }
                File.Move(temp, path, true);
                values = next;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
