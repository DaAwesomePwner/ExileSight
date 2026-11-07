// Provenance: Exile-UI modules/item-checker.ahk:2354–2613; persistence redesigned for per-character JSON.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ExileSight.Features.GearCmp;

public sealed record GearDocument(int SchemaVersion, Dictionary<GearSlot, EquippedGear> Slots);
public sealed record ActiveCharacter(int SchemaVersion, string? Name);
public sealed class GearStore
{
    private readonly string directory;
    private readonly Func<string, string> readText;
    public GearStore(string directory) : this(directory, File.ReadAllText) { }
    internal GearStore(string directory, Func<string, string> readText)
    { this.directory = directory; this.readText = readText; }
    public string? Notice { get; private set; }
    private readonly HashSet<string> blocked = [];
    public string? ReadActive() => Read<ActiveCharacter>("active.json", d => d.SchemaVersion == 1 &&
        (d.Name is null || ValidName(d.Name)))?.Name;
    public void WriteActive(string? name)
    {
        if (name is not null && !ValidName(name)) throw new ArgumentException("Invalid character name");
        Write("active.json", new ActiveCharacter(1, name));
    }
    public static bool ValidName(string name) => name.Length is > 0 and <= 128 && !name.Any(char.IsControl);
    // Hex-encoded hash gives collision-resistant, Windows-safe filenames, including reserved device names.
    public static string ProfileFile(string? name) => name is null ? "default.json" :
        "character-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))) + ".json";
    public Dictionary<GearSlot, EquippedGear> Load(string? name) =>
        Read<GearDocument>(ProfileFile(name), Valid)?.Slots ?? [];
    public void Save(string? name, Dictionary<GearSlot, EquippedGear> slots) =>
        Write(ProfileFile(name), new GearDocument(1, slots));
    private static bool Valid(GearDocument d) => d.SchemaVersion == 1 && d.Slots is not null &&
        d.Slots.Count <= 10 && d.Slots.All(pair => Enum.IsDefined(pair.Key) && pair.Value is { ItemClass: not null, Stats: not null } &&
            pair.Value.ItemClass.Length <= 128 && pair.Value.Stats.Count <= 64 && pair.Value.Stats.Keys.All(k => k.Length <= 128));
    private T? Read<T>(string file, Func<T, bool> validate) where T : class
    {
        var path = Path.Combine(directory, file);
        try
        {
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 1024 * 1024) throw new IOException("Oversized gear file");
            var text = readText(path);
            using var json = JsonDocument.Parse(text);
            if (json.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid gear file");
            if (json.RootElement.TryGetProperty("SchemaVersion", out var version) &&
                version.ValueKind == JsonValueKind.Number && version.TryGetDecimal(out var number) && number > 1)
            {
                blocked.Add(file);
                Notice = "Equipped data uses a newer schema; this file is read-only.";
                return null;
            }
            var document = JsonSerializer.Deserialize<T>(text);
            if (document is null || !validate(document)) throw new JsonException("Invalid gear file");
            return document;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            blocked.Add(file);
            Notice = "Equipped data could not be read; writes disabled for this session. Restart to retry.";
            return null;
        }
        catch (JsonException)
        {
            try { File.Move(path, path + "." + Guid.NewGuid().ToString("N") + ".corrupt"); }
            catch (Exception move) when (move is IOException or UnauthorizedAccessException) { blocked.Add(file); }
            Notice = "Equipped data unavailable; previous file preserved. Re-mark gear after recovery.";
            return null;
        }
    }
    private void Write<T>(string file, T value)
    {
        if (blocked.Contains(file) || blocked.Contains("active.json")) throw new IOException("Previous gear file could not be preserved");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, file);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            if (bytes.Length > 1024 * 1024) throw new IOException("Oversized gear file");
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
