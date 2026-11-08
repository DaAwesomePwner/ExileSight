using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class RealImageFactAttribute : FactAttribute
{
    public RealImageFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EXILESIGHT_REAL_STASH_DIR")))
            Skip = "Set EXILESIGHT_REAL_STASH_DIR to the local, uncommitted image corpus";
    }
}

public sealed class RealImageTests(ITestOutputHelper output)
{
    [RealImageFact]
    public void RealImagesRecoverIdentityAndCalibration()
    {
        var folder = Environment.GetEnvironmentVariable("EXILESIGHT_REAL_STASH_DIR")!;
        var labels = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(Path.Combine(folder, "labels.json")))!;
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        foreach (var (name, expected) in labels.Where(p => p.Key.StartsWith("src", StringComparison.Ordinal)))
        foreach (var width in new[] { 2000, 3440 })
        foreach (var shiftX in expected is null ? new[] { 0 } : new[] { 0, -5, 5 })
        foreach (var shiftY in expected is null ? new[] { 0 } : new[] { 0, -5, 5 })
        {
            var bytes = File.ReadAllBytes(Path.Combine(folder, "bmp", $"{name}-{width}.bmp"));
            var offset = BitConverter.ToInt32(bytes, 10);
            var size = new Resolution(BitConverter.ToInt32(bytes, 18), -BitConverter.ToInt32(bytes, 22));
            Assert.Equal(width, size.Width); Assert.True(size.Height > 0);
            var pixels = new byte[size.Width * size.Height * 4];
            for (var y = 0; y < size.Height; y++)
            for (var x = 0; x < size.Width; x++)
                if (x - shiftX >= 0 && x - shiftX < size.Width && y - shiftY >= 0 && y - shiftY < size.Height)
                    Buffer.BlockCopy(bytes, offset + ((y - shiftY) * size.Width + x - shiftX) * 4,
                        pixels, (y * size.Width + x) * 4, 4);
            var sample = new GridSample(0, 0, size.Width, size.Height, pixels);
            var pre = GridPrecheck.Score(layouts, size, _ => null, a => GridFixture.Crop(pixels, size, a));
            var watch = Stopwatch.StartNew();
            var result = GridIdentifier.Identify(layouts, size, sample, _ => null, pre);
            var elapsed = watch.Elapsed.TotalMilliseconds;
            var actual = result.Layout is null ? null : $"{result.Layout.TabId}/{result.Layout.Page}";
            var layout = layouts.SingleOrDefault(l => $"{l.TabId}/{l.Page}" == expected);
            double error = 0;
            GridLocation? wide = null;
            if (layout is not null)
            {
                var fit = Geometry.Fit(layout.ReferenceResolution, size);
                wide = GridLocator.Locate(layout, size, fit, sample);
                error = wide.Transform is null ? double.PositiveInfinity : layout.Cells.Max(c =>
                {
                    var a = (fit with { X = fit.X + shiftX, Y = fit.Y + shiftY }).Apply(Geometry.Center(layout, c));
                    var b = wide.Transform.Apply(Geometry.Center(layout, c));
                    return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
                });
            }
            output.WriteLine($"{name} {width} shift=({shiftX},{shiftY}): {expected ?? "Unknown"} => {actual ?? "Unknown"}, score={result.Score:F2}, margin={result.Margin:F2}, pre={(layout is null ? pre.Values.Max() : pre[layout]):F2}, error={error:F2}px, {elapsed:F1}ms");
            Assert.Equal(expected, actual);
            if (layout is null) continue;
            Assert.True(result.Margin >= GridIdentifier.MinimumMargin);
            Assert.True(StashWatchPolicy.ShouldIdentify(pre[layout]));
            Assert.NotNull(wide!.Transform);
            Assert.InRange(error, 0, 2);
        }
    }
}
