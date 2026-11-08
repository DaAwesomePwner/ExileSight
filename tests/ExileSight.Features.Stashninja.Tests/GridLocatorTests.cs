using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class GridLocatorTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1, 1, 1.032, 13.3, -11.2)]
    [InlineData(.65, 1.7, .963, -12.4, 14.7)]
    [InlineData(1.2, .7, 1.007, 5.6, 3.2)]
    public void RecoversNoisyPartiallyFilledGrid(double brightness, double gamma, double scale, double x, double y)
    {
        var layout = GridFixture.Layout; var size = new Resolution(1000, 600);
        var expected = new Transform(scale, x, y);
        var pixels = GridFixture.Render(layout, size, expected, brightness, gamma);
        var timer = Stopwatch.StartNew();
        var result = GridLocator.Locate(layout, size, new(1, 0, 0), new(0, 0, size.Width, size.Height, pixels));
        output.WriteLine($"Search: {timer.ElapsedMilliseconds} ms; score {result.Confidence}; {result.Reason}");
        Assert.NotNull(result.Transform);
        Assert.InRange(Math.Abs(result.Transform.Scale / scale - 1), 0, .005);
        foreach (var cell in layout.Cells)
        {
            var a = expected.Apply(Geometry.Center(layout, cell)); var b = result.Transform.Apply(Geometry.Center(layout, cell));
            Assert.InRange(Math.Abs(a.X - b.X), 0, 1); Assert.InRange(Math.Abs(a.Y - b.Y), 0, 1);
        }
        Assert.InRange(result.Evaluations, 1, GridLocator.MaxEvaluations);
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(3440, 1440)]
    [InlineData(3840, 2160)]
    public void RecoversAtClientScaleFromCroppedSamples(int width, int height)
    {
        var layout = GridFixture.Layout; var size = new Resolution(width, height);
        var start = Geometry.Fit(layout.ReferenceResolution, size);
        var expected = new Transform(start.Scale * 1.024, start.X + height * .017, -height * .013);
        var full = GridFixture.Render(layout, size, expected, .8, 1.3);
        var left = (int)(60 * start.Scale + start.X); var top = (int)(65 * start.Scale);
        var cropWidth = (int)(380 * start.Scale); var cropHeight = (int)(340 * start.Scale);
        var crop = new byte[cropWidth * cropHeight * 4];
        for (var row = 0; row < cropHeight; row++)
            Buffer.BlockCopy(full, ((top + row) * width + left) * 4, crop, row * cropWidth * 4, cropWidth * 4);
        var result = GridLocator.Locate(layout, size, start, new(left, top, cropWidth, cropHeight, crop));
        Assert.NotNull(result.Transform);
        Assert.InRange(Math.Abs(result.Transform.Scale / expected.Scale - 1), 0, .005);
        foreach (var cell in layout.Cells)
        {
            var a = expected.Apply(Geometry.Center(layout, cell)); var b = result.Transform.Apply(Geometry.Center(layout, cell));
            Assert.InRange(Math.Abs(a.X - b.X), 0, 1); Assert.InRange(Math.Abs(a.Y - b.Y), 0, 1);
        }
    }

    [Fact]
    public void RefusesNoiseFlatAndUnrelatedImages()
    {
        var layout = GridFixture.Layout; var size = new Resolution(1000, 600);
        var noise = GridFixture.Render(layout, size, new(1, 0, 0), 1, 1, false);
        var unrelated = GridFixture.Render(layout, size, new(.7, 150, 100), 1, 1);
        foreach (var pixels in new[] { noise, unrelated, new byte[1000 * 600 * 4] })
        {
            var result = GridLocator.Locate(layout, size, new(1, 0, 0), new(0, 0, 1000, 600, pixels));
            Assert.Null(result.Transform); Assert.NotEmpty(result.Reason);
            Assert.InRange(result.Evaluations, 0, GridLocator.MaxEvaluations);
        }
    }

    [Fact]
    public void RejectsOversizedOrMalformedSamplesBeforeAllocation()
    {
        Assert.Null(GridLocator.Locate(GridFixture.Layout, new(1000, 600), new(1, 0, 0), new(0, 0, int.MaxValue, 2, [])).Transform);
        Assert.Null(GridLocator.Locate(GridFixture.Layout, new(1000, 600), new(double.NaN, 0, 0), new(0, 0, 2, 2, [])).Transform);
    }
}
