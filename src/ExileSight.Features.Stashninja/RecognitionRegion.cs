using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace ExileSight.Features.Stashninja;

public sealed record RecognitionRegion(Resolution ReferenceResolution, double X, double Y,
    double Width, double Height, Provenance Provenance)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this with { Provenance = new("", "", default) }, Layouts.Json))));
    public bool Intersects(Geometry.Bounds b) => X < b.X + b.Width && X + Width > b.X && Y < b.Y + b.Height && Y + Height > b.Y;
    public Geometry.Bounds Map(Resolution client)
    {
        var fit = Geometry.Fit(ReferenceResolution, client); var start = fit.Apply(new(X, Y));
        return new(start.X, start.Y, Width * fit.Scale, Height * fit.Scale);
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
        return r;
    }
}
