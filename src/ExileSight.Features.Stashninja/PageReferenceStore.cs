using System.Text.Json;
namespace ExileSight.Features.Stashninja;

public sealed class PageReferenceStore
{
    public sealed record Entry(Resolution Resolution, string Page, double[] Signature, double[]? SubSignature);
    public sealed record Document(int Version, string Region, Entry[] Entries);
    private readonly string path, region;
    private Entry[] entries = [];
    public string? Notice { get; private set; }
    public PageReferenceStore(string directory, string region)
    {
        path = Path.Combine(directory, "references.json"); this.region = region;
        try
        {
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new JsonException();
            var doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Layouts.Json);
            if (doc is null || doc.Version != 2 || doc.Region != region || doc.Entries is null || doc.Entries.Length > 256 ||
                doc.Entries.Any(e => e is null || e.Resolution is not { Width: > 0, Height: > 0 } ||
                    string.IsNullOrWhiteSpace(e.Page) || e.Page.Length > 256 || !PageSignature.Valid(e.Signature) ||
                    (e.SubSignature is not null && !PageSignature.Valid(e.SubSignature))) ||
                doc.Entries.GroupBy(e => (e.Resolution, e.Page)).Any(g => g.Count() > 1)) throw new JsonException();
            entries = doc.Entries;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { Notice = "Page references invalid or outdated; teach pages again."; }
    }
    public IReadOnlyDictionary<string, Entry> Get(Resolution resolution) => entries.Where(e => e.Resolution == resolution)
        .ToDictionary(e => e.Page, e => e with { Signature = (double[])e.Signature.Clone(), SubSignature = e.SubSignature is null ? null : (double[])e.SubSignature.Clone() });
    public void Save(Resolution resolution, string page, double[] signature, double[]? subSignature = null)
    {
        if (!PageSignature.Valid(signature) || (subSignature is not null && !PageSignature.Valid(subSignature)) || resolution.Width <= 0 || resolution.Height <= 0 || string.IsNullOrWhiteSpace(page) || page.Length > 256)
            throw new ArgumentException("Invalid page reference");
        Write(entries.Where(e => e.Resolution != resolution || e.Page != page).Append(new(resolution, page, (double[])signature.Clone(), subSignature is null ? null : (double[])subSignature.Clone())).ToArray());
    }
    public void Forget(Resolution resolution, string page) => Write(entries.Where(e => e.Resolution != resolution || e.Page != page).ToArray());
    private void Write(Entry[] next)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Document(2, region, next), Layouts.Json);
        if (next.Length > 256 || bytes.Length > 8 * 1024 * 1024) throw new IOException("Reference store full");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, bytes); File.Move(temp, path, true); entries = next; Notice = null; }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
