using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class RealImageKeepTests(ITestOutputHelper output)
{
    // Owner decision: strict identification takes precedence; these ten half-grid cases hide after two failures.
    private static readonly HashSet<(int Width, string Page)> HalfGridFailures =
    [
        (2000, "fragment/general"),
        (2000, "delve/general"),
        (2000, "blight/general"),
        (2000, "currency/atlas"),
        (2000, "essence/general"),
        (3440, "delve/general"),
        (3440, "blight/general"),
        (3440, "delirium/general"),
        (3440, "essence/general"),
        (3440, "currency/league")
    ];

    [RealImageFact]
    public void RealImageStrictRetentionMatchesOcclusionDecisions()
    {
        var folder = Environment.GetEnvironmentVariable("EXILESIGHT_REAL_STASH_DIR")!;
        var labels = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(Path.Combine(folder, "labels.json")))!;
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var failures = new List<string>();
        foreach (var width in new[] { 2000, 3440 })
        {
            var scenes = labels.Where(p => p.Key.StartsWith("src", StringComparison.Ordinal)).Select(p =>
            {
                var bytes = File.ReadAllBytes(Path.Combine(folder, "bmp", $"{p.Key}-{width}.bmp"));
                var size = new Resolution(width, -BitConverter.ToInt32(bytes, 22));
                return (p.Key, p.Value, Size: size, Sample: new GridSample(0, 0, width, size.Height, bytes.AsSpan(BitConverter.ToInt32(bytes, 10)).ToArray()));
            }).ToArray();
            foreach (var scene in scenes.Where(s => s.Value is not null))
            {
                var layout = layouts.Single(l => $"{l.TabId}/{l.Page}" == scene.Value);
                var transform = GridLocator.Locate(layout, scene.Size, Geometry.Fit(layout.ReferenceResolution, scene.Size), scene.Sample).Transform!;
                Assert.NotNull(transform);
                var results = new Dictionary<GridSample, GridIdentification>();
                GridIdentification Identify(GridSample sample)
                {
                    if (!results.TryGetValue(sample, out var result))
                        results[sample] = result = GridIdentifier.Identify(layouts, scene.Size, sample,
                            l => l == layout ? transform : Geometry.Fit(l.ReferenceResolution, scene.Size), buttons: GridFixture.Buttons);
                    return result;
                }
                bool Keep(GridSample sample) => Identify(sample).Layout == layout || Identify(sample).HoverPage == layout;
                var bounds = layout.Cells.Select(c => (P: transform.Apply(Layouts.Origin(layout, c)), W: c.W * transform.Scale, H: c.H * transform.Scale)).ToArray();
                var right = (int)Math.Ceiling(bounds.Max(b => b.P.X + b.W) + 8);
                var bottom = (int)Math.Ceiling(bounds.Max(b => b.P.Y + b.H) + 8);
                var top = (int)Math.Floor(bounds.Min(b => b.P.Y) - 8);
                // A right-hand tooltip leaves at least half the complete cells untouched.
                var cut = (int)Math.Ceiling(bounds.OrderBy(b => b.P.X + b.W).ElementAt((bounds.Length + 1) / 2 - 1).P.X +
                    bounds.OrderBy(b => b.P.X + b.W).ElementAt((bounds.Length + 1) / 2 - 1).W + 8);
                GridSample Paint(int left, int y0, int x1, int y1)
                {
                    var pixels = (byte[])scene.Sample.Bgra.Clone();
                    for (var y = y0; y < y1; y++)
                    for (var x = left; x < x1; x++)
                    {
                        var n = (y * width + x) * 4;
                        pixels[n] = pixels[n + 1] = pixels[n + 2] = (byte)((x % 13 < 3 && y % 17 < 2) ? 170 : 8);
                    }
                    return scene.Sample with { Bgra = pixels };
                }
                var header = Paint(0, 0, right, top);
                var partial = Paint(cut, 0, right, bottom);
                var total = Paint(0, 0, right, bottom);
                foreach (var (name, sample, expected) in new[] { ("clean", scene.Sample, true), ("header", header, true), ("partial", partial, !HalfGridFailures.Contains((width, scene.Value!))), ("total", total, false) })
                {
                    output.WriteLine($"{width} {scene.Value} {name}: {Identify(sample).Score:F2}");
                    if (Keep(sample) != expected) failures.Add($"{width}/{scene.Value}/{name}");
                    if (!expected) AssertTwoFailuresAndRecovery(Identify(sample), Identify(scene.Sample), layout);
                    output.WriteLine($"POLICY {width}/{scene.Value}/{name}: {(expected ? "keep" : "hide after two; pass resets")}");
                }
                var wrong = scenes.Where(s => s.Value != scene.Value).Select(s => (s.Key, Score: Identify(s.Sample).Score, Keep: Keep(s.Sample))).ToArray();
                foreach (var other in wrong.Where(s => s.Keep)) failures.Add($"{width}/{scene.Value} kept {other.Key} ({other.Score:F2})");
                var worldPixels = (byte[])scene.Sample.Bgra.Clone();
                for (var y = 0; y < bottom; y++)
                    Buffer.BlockCopy(scene.Sample.Bgra, (y * width + width / 2) * 4, worldPixels, y * width * 4, right * 4);
                if (Keep(scene.Sample with { Bgra = worldPixels })) failures.Add($"{width}/{scene.Value}/world");
                output.WriteLine($"{width} {scene.Value}: best other winner={wrong.Max(s => s.Score):F2}");
                if (scene.Key == "src6")
                {
                    var pixels = (byte[])scene.Sample.Bgra.Clone();
                    GridFixture.BrightenButton(pixels, scene.Size, "league", 25);
                    var hover = scene.Sample with { Bgra = pixels };
                    output.WriteLine($"HOVER {width}: fit={GridFixture.Buttons.Identify(scene.Size, hover)}, calibrated={GridFixture.Buttons.Identify(scene.Size, hover, transform)}");
                    // Owner decision (both widths): +25 may name League; never show League prices on Atlas.
                    Assert.NotEqual("league", Identify(hover).Layout?.Page);
                    AssertTwoFailuresAndRecovery(Identify(hover), Identify(scene.Sample), layout);
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
    private static void AssertTwoFailuresAndRecovery(GridIdentification failure, GridIdentification pass, Layout page)
    {
        Assert.Null(failure.Layout); // Unknown, not a wrong page.
        Assert.Null(failure.HoverPage);
        Assert.Equal(page, pass.Layout);
        var baseline = new double[PageSignature.Length];
        var changed = Enumerable.Repeat(.2, PageSignature.Length).ToArray();
        var now = DateTimeOffset.UnixEpoch;
        var policy = new StashWatchPolicy(); policy.Accept(baseline, baseline);
        Assert.Equal(WatchWork.None, policy.Tick(now, changed, changed).Work);
        Assert.Equal(WatchWork.Identify, policy.Tick(now.AddMilliseconds(450), changed, changed).Work);
        Assert.False(policy.Retain(now.AddMilliseconds(450), false, changed, changed));
        Assert.True(policy.Identified); // The occluded check counts as exactly one failure.
        Assert.True(policy.Retain(now.AddMilliseconds(1350), false, changed, changed));
        Assert.False(policy.Identified);
        policy.Accept(baseline, baseline); // A passing check restores the displayed page.
        Assert.False(policy.Retain(now.AddSeconds(2), false, changed, changed));
        Assert.False(policy.Retain(now.AddSeconds(2.5), pass.Layout == page, baseline, baseline));
        Assert.True(policy.Identified);
        Assert.False(policy.Retain(now.AddSeconds(3), false, changed, changed)); // Pass reset the count.
        Assert.True(policy.Identified);
        Assert.True(policy.Retain(now.AddSeconds(3.9), false, changed, changed));
        Assert.False(policy.Identified);
    }
}
