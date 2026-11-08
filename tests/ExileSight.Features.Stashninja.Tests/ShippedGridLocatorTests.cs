using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class ShippedGridLocatorTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("currency-general", 1920, 1080)]
    [InlineData("currency-general", 3440, 1440)]
    [InlineData("essence", 1920, 1080)]
    [InlineData("essence", 3440, 1440)]
    public void RecoversShippedGeometryAtFitWithClientClippedCapture(string name, int width, int height)
    {
        var layout = Layouts.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "layouts", name + ".layout.json")));
        var client = new Resolution(width, height); var expected = Geometry.Fit(layout.ReferenceResolution, client);
        var area = GridSampling.Bounds(layout, client, expected);
        // Render only the capture region, keeping the shipped physical positions and clipping.
        var pixels = GridFixture.Render(layout, new(area.Width, area.Height),
            expected with { X = expected.X - area.X, Y = expected.Y - area.Y }, .8, 1.3);
        var timer = Stopwatch.StartNew();
        var result = GridLocator.Locate(layout, client, expected, new(area.X, area.Y, area.Width, area.Height, pixels));
        output.WriteLine($"{name} {width}x{height}: {timer.ElapsedMilliseconds} ms, confidence {result.Confidence}, {result.Reason}");
        Assert.NotNull(result.Transform);
        Assert.InRange(Math.Abs(result.Transform.Scale / expected.Scale - 1), 0, .005);
        foreach (var cell in layout.Cells)
        {
            var a = expected.Apply(Geometry.Center(layout, cell)); var b = result.Transform.Apply(Geometry.Center(layout, cell));
            Assert.InRange(Math.Abs(a.X - b.X), 0, 1); Assert.InRange(Math.Abs(a.Y - b.Y), 0, 1);
        }
    }

    [Fact]
    public void RefusesWhenNoCandidateCanBeEvaluated()
    {
        var result = GridLocator.Locate(GridFixture.Layout, new(1000, 600), new(1, 0, 0), new(0, 0, 8, 8, new byte[256]));
        Assert.Null(result.Transform); Assert.True(double.IsFinite(result.Confidence)); Assert.NotEmpty(result.Reason);
    }
}
