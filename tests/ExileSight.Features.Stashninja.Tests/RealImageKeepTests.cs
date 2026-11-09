using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class RealImageKeepTests(ITestOutputHelper output)
{
    [RealImageFact]
    public void RealImageKeepRejectsOtherScenesAndToleratesPartialOcclusion()
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
                bool Keep(GridSample sample) => GridKeep.Supports(layout, transform, scene.Size, sample, GridFixture.Buttons);
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
                foreach (var (name, sample, expected) in new[] { ("clean", scene.Sample, true), ("header", header, true), ("partial", partial, true), ("total", total, false) })
                {
                    output.WriteLine($"{width} {scene.Value} {name}: {GridKeep.Score(layout, transform, sample):F2}");
                    var policy = new StashWatchPolicy();
                    var baseline = new double[PageSignature.Length];
                    var changed = Enumerable.Repeat(.5, PageSignature.Length).ToArray();
                    policy.Accept(baseline, baseline);
                    Assert.Equal(WatchWork.None, policy.Tick(DateTimeOffset.UnixEpoch, changed, changed).Work);
                    Assert.Equal(WatchWork.Identify, policy.Tick(DateTimeOffset.UnixEpoch.AddMilliseconds(450), changed, changed).Work);
                    Assert.False(policy.Retain(DateTimeOffset.UnixEpoch, Keep(sample), changed, changed));
                    Assert.Equal(!expected, policy.Retain(DateTimeOffset.UnixEpoch, Keep(sample), changed, changed));
                    Assert.Equal(expected, policy.Identified);
                    if (expected) Assert.Equal(WatchWork.None, policy.Tick(DateTimeOffset.UnixEpoch, changed, changed).Work);
                    var area = GridKeep.Bounds(layout, transform, scene.Size, GridFixture.Buttons);
                    var crop = GridFixture.Crop(sample.Bgra, scene.Size, area);
                    Assert.Equal(Keep(sample), Keep(crop!));
                    if (Keep(sample) != expected) failures.Add($"{width}/{scene.Value}/{name}");
                }
                var wrong = scenes.Where(s => s.Value != scene.Value).Select(s => (s.Key, Score: GridKeep.Score(layout, transform, s.Sample), Keep: Keep(s.Sample))).ToArray();
                foreach (var other in wrong.Where(s => s.Keep)) failures.Add($"{width}/{scene.Value} kept {other.Key} ({other.Score:F2})");
                var worldPixels = (byte[])scene.Sample.Bgra.Clone();
                for (var y = 0; y < bottom; y++)
                    Buffer.BlockCopy(scene.Sample.Bgra, (y * width + width / 2) * 4, worldPixels, y * width * 4, right * 4);
                if (Keep(scene.Sample with { Bgra = worldPixels })) failures.Add($"{width}/{scene.Value}/world");
                output.WriteLine($"{width} {scene.Value}: best other={wrong.Max(s => s.Score):F2}, masked={GridKeep.Score(layout, transform, scene.Sample, true):F2}");
                if (scene.Key == "src6")
                {
                    var pixels = (byte[])scene.Sample.Bgra.Clone();
                    GridFixture.BrightenButton(pixels, scene.Size, "league", 25);
                    var hover = scene.Sample with { Bgra = pixels };
                    output.WriteLine($"HOVER {width}: fit={GridFixture.Buttons.Identify(scene.Size, hover)}, calibrated={GridFixture.Buttons.Identify(scene.Size, hover, transform)}");
                    if (!Keep(hover)) failures.Add($"{width}/atlas-hover");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
