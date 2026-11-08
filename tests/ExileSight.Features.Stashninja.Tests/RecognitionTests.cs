using System.Text.Json;
using ExileSight.Features.Stashninja;
using Xunit;
namespace ExileSight.Features.Stashninja.Tests;

public sealed class RecognitionTests
{
    private sealed record Case(int Seed, int Shift, int Noise, bool Matches, double Gamma = 1);
    private static double[] Signature(int seed, int shift = 0, int noise = 0, double gamma = 1)
    {
        var random = new Random(seed); var jitter = new Random(8);
        var pixels = new byte[96 * 12 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        for (var c = 0; c < 3; c++) pixels[i + c] = (byte)(random.Next(40, 180) + shift + jitter.Next(-noise, noise + 1));
        for (var i = 0; i < pixels.Length; i++) pixels[i] = (byte)Math.Round(255 * Math.Pow(pixels[i] / 255d, gamma));
        return PageSignature.Create(96, 12, pixels);
    }
    [Fact]
    public void SyntheticPagesMatchOnlyWithConfidence()
    {
        var reference = Signature(17);
        var cases = JsonSerializer.Deserialize<Case[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "recognition-cases.json")), Layouts.Json)!;
        foreach (var c in cases)
            Assert.Equal(c.Matches ? "page" : null, PageSignature.Match(Signature(c.Seed, c.Shift, c.Noise, c.Gamma),
                new Dictionary<string, double[]> { ["page"] = reference }));
        Assert.Null(PageSignature.Match(reference, new Dictionary<string, double[]>()));
        Assert.Null(PageSignature.Match(reference, new Dictionary<string, double[]> { ["a"] = reference, ["b"] = reference }));
    }
    [Fact]
    public void ReferencesRoundTripForgetAndIgnoreCorruption()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var store = new PageReferenceStore(directory, "region-v1"); var resolution = new Resolution(2000, 837);
            store.Save(resolution, "currency/general", Signature(17), Signature(29));
            var loaded = new PageReferenceStore(directory, "region-v1");
            Assert.Single(loaded.Get(resolution));
            Assert.Equal(Signature(29), loaded.Get(resolution)["currency/general"].SubSignature);
            Assert.Empty(loaded.Get(new(1000, 837)));
            Assert.Empty(new PageReferenceStore(directory, "region-v2").Get(resolution));
            File.WriteAllText(Path.Combine(directory, "references.json"),
                JsonSerializer.Serialize(new PageReferenceStore.Document(1, "region-v1", []), Layouts.Json));
            Assert.NotNull(new PageReferenceStore(directory, "region-v1").Notice);
            loaded.Forget(resolution, "currency/general");
            Assert.Empty(new PageReferenceStore(directory, "region-v1").Get(resolution));
            File.WriteAllText(Path.Combine(directory, "references.json"), "{broken");
            var corrupt = new PageReferenceStore(directory, "region-v1");
            Assert.NotNull(corrupt.Notice); Assert.Empty(corrupt.Get(resolution));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public void InvalidSignaturesAndRegionsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => PageSignature.Create(1, 1, new byte[4]));
        Assert.Null(PageSignature.Match([double.NaN], new Dictionary<string, double[]>()));
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "region.json");
            File.WriteAllText(path, "null");
            Assert.Throws<JsonException>(() => RecognitionRegion.Load(path, []));
            File.WriteAllText(Path.Combine(directory, "references.json"),
                "{\"version\":1,\"region\":\"v1\",\"entries\":[null]}");
            var store = new PageReferenceStore(directory, "v1");
            Assert.NotNull(store.Notice); Assert.Empty(store.Get(new(2000, 837)));
            Assert.Throws<ArgumentException>(() => store.Save(new(2000, 837), "page", [double.NaN]));
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void SubRegionChangesInvalidateReferencesAndOwnTabOverlapIsRejected()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var region = RecognitionRegion.Load(Path.Combine(AppContext.BaseDirectory, "layouts", "recognition.json"), layouts);
        var changed = region with { SubTabs = new(region.SubTabs) };
        changed.SubTabs["currency"] = changed.SubTabs["currency"] with { Height = 39 };
        Assert.NotEqual(region.Fingerprint, changed.Fingerprint);
        Assert.NotEqual(region.Fingerprint, (region with { Height = region.Height - 1 }).Fingerprint);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            changed.SubTabs["currency"] = changed.SubTabs["currency"] with { Height = 200 };
            File.WriteAllText(path, JsonSerializer.Serialize(changed, Layouts.Json));
            Assert.Throws<JsonException>(() => RecognitionRegion.Load(path, layouts));
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void RegionAvoidsEveryShippedCellAndTag()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var region = RecognitionRegion.Load(Path.Combine(AppContext.BaseDirectory, "layouts", "recognition.json"), layouts);
        foreach (var pair in region.SubTabs)
        foreach (var layout in layouts.Where(l => l.TabId == pair.Key))
        foreach (var cell in layout.Cells)
            Assert.False(RecognitionRegion.Intersects(pair.Value, Geometry.TagBounds(layout, cell, new(1, 0, 0))));
        foreach (var layout in layouts)
        foreach (var cell in layout.Cells)
            Assert.False(region.Intersects(Geometry.TagBounds(layout, cell, new(1, 0, 0))));
    }
}
