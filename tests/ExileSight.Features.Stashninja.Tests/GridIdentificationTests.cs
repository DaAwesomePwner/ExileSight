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
        for (var i = 0; i < 100; i++) PageSignature.Create(825, 155, pixels);
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
        {
            var size = new Resolution(scene.Width, scene.Height);
            var pixels = GridFixture.Render(layout, size, Geometry.Fit(layout.ReferenceResolution, size), scene.Brightness, scene.Gamma);
            var watch = Stopwatch.StartNew();
            var result = GridIdentifier.Identify(layouts, size, new(0, 0, size.Width, size.Height, pixels), _ => null);
            output.WriteLine($"{layout.TabId}/{layout.Page} {size}: {watch.ElapsedMilliseconds}ms, {result.Score:F2}, margin {result.Margin:F2}");
            Assert.Equal(layout, result.Layout);
            Assert.NotNull(result.Transform);
        }
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
