using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace ExileSight.Features.MapTracker;

public interface IRunStore
{
    IReadOnlyList<MapRun> Read(string league);
    void Save(MapRun run);
}

/// <summary>Atomic bounded snapshots; reaching the cap refuses writes without discarding history.</summary>
public sealed class RunStore(string directory, int limitBytes = 2 * 1024 * 1024) : IRunStore
{
    public string PathFor(string league) => Path.Combine(directory,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(league))) + ".jsonl");
    public IReadOnlyList<MapRun> Read(string league)
    {
        var path = PathFor(league);
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > limitBytes) throw new IOException("Oversized history is read-only.");
        try
        {
            var lines = File.ReadAllLines(path);
            // Check schema before binding current fields: a future format may have entirely different fields.
            foreach (var line in lines)
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("Schema", out var schema) && schema.ValueKind == JsonValueKind.Number && schema.TryGetInt32(out var version) && version > 1)
                    throw new NotSupportedException("Newer map tracker schema: read-only.");
            }
            var rows = lines.Select(line => JsonSerializer.Deserialize<MapRun>(line)
                ?? throw new JsonException()).ToArray();
            if (rows.Any(r => r.Schema > 1)) throw new NotSupportedException("Newer map tracker schema: read-only.");
            if (rows.Any(r => r.Schema != 1 || r.League != league || r.Map is null || r.Mods is null
                || r.Mechanics is null || r.Notes is null || r.Seconds < 0 || !double.IsFinite(r.Seconds))) throw new JsonException();
            return rows;
        }
        catch (JsonException) { Quarantine(path); return []; }
    }
    public void Save(MapRun run)
    {
        Directory.CreateDirectory(directory);
        var existing = Read(run.League);
        var prior = existing.FirstOrDefault(r => r.Id == run.Id);
        run = run with { Extra = prior?.Extra ?? run.Extra };
        var rows = existing.Where(r => r.Id != run.Id).Append(run).ToList();
        var path = PathFor(run.League);
        string Encode() => string.Join('\n', rows.Select(r => JsonSerializer.Serialize(r))) + "\n";
        var text = Encode();
        if (Encoding.UTF8.GetByteCount(text) > limitBytes)
            throw new IOException("History reached its size cap; recording is read-only. Existing rows are preserved.");
        var temporary = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write))
            { stream.Write(Encoding.UTF8.GetBytes(text)); stream.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Quarantine(string path) => File.Move(path, path + ".corrupt", true);
}
