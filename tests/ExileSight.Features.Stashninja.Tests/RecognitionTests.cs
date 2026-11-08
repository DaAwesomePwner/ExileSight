using System.Text.Json;
using ExileSight.Features.Stashninja;
using Xunit;
namespace ExileSight.Features.Stashninja.Tests;

public sealed class RecognitionTests
{
    private sealed record Case(int Seed, int Shift, int Noise, bool Matches);
    private static double[] Signature(int seed, int shift = 0, int noise = 0)
    {
        var random = new Random(seed); var jitter = new Random(8);
        var pixels = new byte[96 * 12 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        for (var c = 0; c < 3; c++) pixels[i + c] = (byte)(random.Next(40, 180) + shift + jitter.Next(-noise, noise + 1));
        return PageSignature.Create(96, 12, pixels);
    }
    [Fact]
    public void SyntheticPagesMatchOnlyWithConfidence()
    {
        var reference = Signature(17);
        var cases = JsonSerializer.Deserialize<Case[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "recognition-cases.json")), Layouts.Json)!;
        foreach (var c in cases)
            Assert.Equal(c.Matches ? "page" : null, PageSignature.Match(Signature(c.Seed, c.Shift, c.Noise),
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
            store.Save(resolution, "currency/general", Signature(17));
            var loaded = new PageReferenceStore(directory, "region-v1");
            Assert.Single(loaded.Get(resolution));
            Assert.Empty(loaded.Get(new(1000, 837)));
            Assert.Empty(new PageReferenceStore(directory, "region-v2").Get(resolution));
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
    public void RegionAvoidsEveryShippedCellAndTag()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var region = RecognitionRegion.Load(Path.Combine(AppContext.BaseDirectory, "layouts", "recognition.json"), layouts);
        foreach (var layout in layouts)
        foreach (var cell in layout.Cells)
            Assert.False(region.Intersects(Geometry.TagBounds(layout, cell, new(1, 0, 0))));
    }
}
