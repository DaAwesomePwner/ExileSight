using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class GridIdentificationTests(ITestOutputHelper output)
{
    private sealed record Scene(int Width, int Height, double Brightness, double Gamma);
    [Fact]
    public void HeaderSignatureTiming()
    {
        var pixels = new byte[825 * 155 * 4];
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            var signature = PageSignature.Create(825, 155, pixels);
            Assert.Equal(PageSignature.Length, signature.Length);
            Assert.True(PageSignature.Valid(signature));
        }
        Assert.Throws<ArgumentException>(() => PageSignature.Create(2048, 2048, []));
        output.WriteLine($"Ultrawide header signature: {watch.Elapsed.TotalMilliseconds / 100:F3} ms/check (capture excluded)");
    }

    [Fact]
    public void EveryShippedPageIdentifiesOnlyAsItself()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var scenes = JsonSerializer.Deserialize<Scene[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "grid-scenes.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach (var scene in scenes)
        foreach (var layout in layouts)
        foreach (var offset in new[] { -2, 2 })
        {
            var size = new Resolution(scene.Width, scene.Height);
            var fit = Geometry.Fit(layout.ReferenceResolution, size);
            var pixels = GridFixture.Render(layout, size, fit with { X = fit.X + offset, Y = offset },
                scene.Brightness, scene.Gamma, distractors: true);
            var precheck = GridPrecheck.Score(layouts, size, _ => null, area => GridFixture.Crop(pixels, size, area));
            Assert.True(StashWatchPolicy.ShouldIdentify(precheck[layout]),
                $"{layout.TabId}/{layout.Page} sparse score {precheck[layout]} at offset {offset}");
            var watch = Stopwatch.StartNew();
            var result = GridIdentifier.Identify(layouts, size, new(0, 0, size.Width, size.Height, pixels), _ => null);
            output.WriteLine($"{layout.TabId}/{layout.Page} {size}: {watch.ElapsedMilliseconds}ms, {result.Score:F2}, margin {result.Margin:F2}");
            Assert.Equal(layout, result.Layout);
            Assert.NotNull(result.Transform);
            Assert.Equal(0, result.BroadSearches);
            Assert.InRange(result.Evaluations, 1, layouts.Length * 559);
            if (layout.TabId == "currency") Assert.True(result.Margin >= 10);
        }
    }
    [Fact]
    public void SparseGateRejectsFlatAndRecoversFirstUseOffset()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var page = layouts.First(l => l.TabId == "currency" && l.Page == "general");
        var size = new Resolution(1920, 1080);
        var fit = Geometry.Fit(page.ReferenceResolution, size);
        var pixels = GridFixture.Render(page, size, fit with { X = fit.X + 8, Y = 6 }, .8, 1.3);
        var calls = 0; var pixelCount = 0;
        GridSample Capture(GridArea a)
        {
            calls++; pixelCount += a.Width * a.Height;
            var crop = new byte[a.Width * a.Height * 4];
            for (var y = 0; y < a.Height; y++)
                Buffer.BlockCopy(pixels, ((a.Y + y) * size.Width + a.X) * 4, crop, y * a.Width * 4, a.Width * 4);
            return new(a.X, a.Y, a.Width, a.Height, crop);
        }
        var watch = Stopwatch.StartNew();
        var precheck = GridPrecheck.Score(layouts, size, _ => null, Capture);
        output.WriteLine($"Sparse precheck: {watch.ElapsedMilliseconds}ms, {pixelCount} pixels, {calls} calls");
        Assert.InRange(calls, 1, layouts.Length * GridPrecheck.ProbesPerLayout);
        Assert.InRange(pixelCount, 1, layouts.Length * GridPrecheck.MaxPixelsPerLayout);
        Assert.True(StashWatchPolicy.ShouldIdentify(precheck[page]));
        watch.Restart();
        var result = GridIdentifier.Identify(layouts, size, new(0, 0, size.Width, size.Height, pixels), _ => null, precheck);
        output.WriteLine($"First-use search: {watch.ElapsedMilliseconds}ms, {result.Evaluations} evaluations, {result.BroadSearches} broad candidates");
        Assert.Equal(page, result.Layout);
        Assert.InRange(result.BroadSearches, 1, 2);
        Assert.InRange(result.Evaluations, 1, layouts.Length * 559 + 2 * GridLocator.MaxEvaluations);
        pixels = GridFixture.Render(page, size, fit, 1, 1, grid: false);
        Assert.All(GridPrecheck.Score(layouts, size, _ => null, Capture).Values,
            score => Assert.False(StashWatchPolicy.ShouldIdentify(score)));
        Assert.All(GridPrecheck.Score(layouts, size, _ => null,
            a => new(a.X, a.Y, a.Width, a.Height, new byte[a.Width * a.Height * 4])).Values,
            score => Assert.False(StashWatchPolicy.ShouldIdentify(score)));
    }

    [Fact]
    public void SavedCalibrationSupportsDisplacedGrids()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var page = layouts[0]; var size = new Resolution(1920, 1080);
        var fit = Geometry.Fit(page.ReferenceResolution, size);
        var saved = fit with { X = fit.X + 20, Y = fit.Y + 12 };
        var pixels = GridFixture.Render(page, size, saved, .8, 1.3);
        Assert.Equal(page, GridIdentifier.Identify(layouts, size,
            new(0, 0, size.Width, size.Height, pixels), _ => saved).Layout);
    }

    [Fact]
    public void FlatNoiseAndAmbiguousPagesNeverSelect()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var size = new Resolution(1920, 1080);
        foreach (var pixels in new[] { new byte[size.Width * size.Height * 4],
            GridFixture.Render(layouts[0], size, new(1, 0, 0), 1, 1, false),
            GridFixture.Render(GridFixture.Layout, size, new(1, 0, 0), 1, 1) })
            Assert.Null(GridIdentifier.Identify(layouts, size, new(0, 0, size.Width, size.Height, pixels), _ => null).Layout);
        var page = layouts[0];
        var image = GridFixture.Render(page, size, Geometry.Fit(page.ReferenceResolution, size), 1, 1);
        Assert.Null(GridIdentifier.Identify([page, page with { Page = "duplicate" }], size,
            new(0, 0, size.Width, size.Height, image), _ => null).Layout);
    }
}
