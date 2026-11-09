using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace ExileSight.Features.Stashninja;

public sealed record RecognitionRegion(Resolution ReferenceResolution, double X, double Y,
    double Width, double Height, Provenance Provenance, Dictionary<string, Geometry.Bounds> SubTabs)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this with { Provenance = new("", "", default) }, Layouts.Json))));
    public bool Intersects(Geometry.Bounds b) => X < b.X + b.Width && X + Width > b.X && Y < b.Y + b.Height && Y + Height > b.Y;
    public static bool Intersects(Geometry.Bounds a, Geometry.Bounds b) =>
        a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;
    public Geometry.Bounds Map(Resolution client, Geometry.Bounds? sub = null)
    {
        var bounds = sub ?? new(X, Y, Width, Height);
        var fit = Geometry.Fit(ReferenceResolution, client); var start = fit.Apply(new(bounds.X, bounds.Y));
        return new(start.X, start.Y, bounds.Width * fit.Scale, bounds.Height * fit.Scale);
    }
    public static RecognitionRegion Load(string path, Layout[] layouts)
    {
        if (new FileInfo(path).Length > 16384) throw new JsonException("Recognition region too large");
        var r = JsonSerializer.Deserialize<RecognitionRegion>(File.ReadAllText(path), Layouts.Json) ?? throw new JsonException();
        if (r.ReferenceResolution is not { Width: > 0, Height: > 0 } || !Layouts.Finite(r.X, r.Y, r.Width, r.Height) ||
            r.X < 0 || r.Y < 0 || r.Width < PageSignature.Columns || r.Height < PageSignature.Rows ||
            r.X + r.Width > r.ReferenceResolution.Width || r.Y + r.Height > r.ReferenceResolution.Height ||
            r.Provenance is null || string.IsNullOrWhiteSpace(r.Provenance.Author) || string.IsNullOrWhiteSpace(r.Provenance.Method) || r.Provenance.Date == default)
            throw new JsonException("Invalid recognition region");
        foreach (var l in layouts)
        {
            if (l.ReferenceResolution != r.ReferenceResolution) throw new JsonException("Mismatched reference resolution");
            foreach (var c in l.Cells)
            {
                var p = Layouts.Origin(l, c);
                if (r.Intersects(new(p.X, p.Y, c.W, c.H))) throw new JsonException("Recognition overlaps a cell");
            }
        }
        if (r.SubTabs is null) throw new JsonException("Missing sub-tab regions");
        foreach (var group in layouts.GroupBy(l => l.TabId).Where(g => g.Count() > 1))
            if (!r.SubTabs.ContainsKey(group.Key)) throw new JsonException("Missing sibling region");
        foreach (var (tab, b) in r.SubTabs)
        {
            if (string.IsNullOrWhiteSpace(tab) || b is null || !Layouts.Finite(b.X, b.Y, b.Width, b.Height) ||
                b.X < 0 || b.Y < 0 || b.Width < PageSignature.Columns || b.Height < PageSignature.Rows ||
                b.X + b.Width > r.ReferenceResolution.Width || b.Y + b.Height > r.ReferenceResolution.Height)
                throw new JsonException("Invalid sub-tab region");
            foreach (var l in layouts.Where(l => l.TabId == tab))
            foreach (var c in l.Cells)
            {
                var p = Layouts.Origin(l, c);
                if (Intersects(b, new(p.X, p.Y, c.W, c.H))) throw new JsonException("Sub-tab region overlaps own tab cell");
            }
        }
        return r;
    }
}
