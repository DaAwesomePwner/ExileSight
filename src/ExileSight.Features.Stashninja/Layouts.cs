using System.Text.Json;

namespace ExileSight.Features.Stashninja;

public sealed record Point(double X, double Y);
public sealed record Resolution(int Width, int Height);
public sealed record Provenance(string Author, string Method, DateOnly Date);
public sealed record Cell(string Id, double X, double Y, double W, double H,
    string Category, string? NinjaId = null, string? ItemName = null, string? Folder = null);
public sealed record Layout(string TabId, string Page, Resolution ReferenceResolution,
    Cell[] Cells, Dictionary<string, Point> FolderOffsets, Provenance Provenance);

public static class Layouts
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { RespectRequiredConstructorParameters = true, WriteIndented = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public static Layout Parse(string json)
    {
        var layout = JsonSerializer.Deserialize<Layout>(json, Json) ?? throw new JsonException("Missing layout");
        Validate(layout);
        return layout;
    }
    public static void Validate(Layout l)
    {
        if (string.IsNullOrWhiteSpace(l.TabId) || string.IsNullOrWhiteSpace(l.Page) ||
            l.ReferenceResolution is not { Width: > 0 and <= 16384, Height: > 0 and <= 16384 } ||
            l.Cells is null || l.Cells.Length is < 2 or > 2000 || l.FolderOffsets is null ||
            l.Provenance is null || string.IsNullOrWhiteSpace(l.Provenance.Author) || l.Provenance.Date == default)
            throw new JsonException("Invalid layout metadata");
        if (l.Provenance.Method != $"measured from screenshot at {l.ReferenceResolution.Width}x{l.ReferenceResolution.Height}")
            throw new JsonException("Screenshot provenance must match reference resolution");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (l.Cells.Where(c => c is not null).Select(c => (c.X + c.W / 2, c.Y + c.H / 2, c.Folder)).Distinct().Count() < 2)
            throw new JsonException("Calibration needs distinct cell centers");
        foreach (var offset in l.FolderOffsets.Values)
            if (offset is null || !Finite(offset.X, offset.Y)) throw new JsonException("Invalid folder offset");
        foreach (var c in l.Cells)
        {
            if (c is null || string.IsNullOrWhiteSpace(c.Id) || !ids.Add(c.Id) ||
                !Finite(c.X, c.Y, c.W, c.H) || c.W <= 0 || c.H <= 0 ||
                (string.IsNullOrWhiteSpace(c.NinjaId) && string.IsNullOrWhiteSpace(c.ItemName)) ||
                !Categories.Contains(c.Category) || (c.Folder is not null && !l.FolderOffsets.ContainsKey(c.Folder)))
                throw new JsonException("Invalid or duplicate cell");
            var origin = Origin(l, c);
            if (origin.X < 0 || origin.Y < 0 || origin.X + c.W > l.ReferenceResolution.Width ||
                origin.Y + c.H > l.ReferenceResolution.Height) throw new JsonException("Cell outside screenshot");
        }
    }
    public static readonly string[] Categories = ["Currency", "Fragment", "Scarab", "Fossil", "Resonator", "Essence", "Oil", "DeliriumOrb", "AllflameEmber"];
    public static bool Finite(params double[] values) => values.All(double.IsFinite);
    public static Point Origin(Layout l, Cell c)
    {
        var offset = c.Folder is null ? new Point(0, 0) : l.FolderOffsets[c.Folder];
        return new(c.X + offset.X, c.Y + offset.Y);
    }
    public static Layout[] LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        var layouts = Directory.GetFiles(directory, "*.layout.json").Select(path =>
        {
            if (new FileInfo(path).Length > 1024 * 1024) throw new JsonException("Layout exceeds size limit");
            return Parse(File.ReadAllText(path));
        }).ToArray();
        if (layouts.GroupBy(l => (l.TabId, l.Page)).Any(g => g.Count() > 1)) throw new JsonException("Duplicate tab/page");
        return layouts;
    }
}
