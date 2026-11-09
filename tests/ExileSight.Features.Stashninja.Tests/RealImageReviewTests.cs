using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.Stashninja.Tests;

// Regression scenarios adapted from the reviewer's ReviewExperimentTests and ReviewOffsetTests.
public sealed class RealImageReviewTests(ITestOutputHelper output)
{
    [RealImageFact]
    public void RealImageOffsetsAndPaintedPanelsNeverRetainWrongPage()
    {
        var folder = Environment.GetEnvironmentVariable("EXILESIGHT_REAL_STASH_DIR")!;
        var labels = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(Path.Combine(folder, "labels.json")))!;
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var failures = new List<string>();
        var pairs = new[] { ("currency/league", "src11"), ("currency/league", "src3"), ("currency/league", "src8"), ("currency/league", "src4"), ("currency/general", "src1"), ("delve/general", "src10"), ("blight/general", "src2"), ("fragment/general", "src3"), ("fragment/general", "src8"), ("fragment/general", "src11") };
        foreach (var width in new[] { 2000, 3440 })
        {
            var scenes = labels.Where(p => p.Key.StartsWith("src", StringComparison.Ordinal)).ToDictionary(p => p.Key, p =>
            {
                var bytes = File.ReadAllBytes(Path.Combine(folder, "bmp", $"{p.Key}-{width}.bmp"));
                return new GridSample(0, 0, width, -BitConverter.ToInt32(bytes, 22), bytes.AsSpan(BitConverter.ToInt32(bytes, 10)).ToArray());
            });
            var size = new Resolution(width, scenes.First().Value.Height);
            var saved = layouts.ToDictionary(l => l, l => GridLocator.Locate(l, size, Geometry.Fit(l.ReferenceResolution, size),
                scenes[labels.First(p => p.Value == $"{l.TabId}/{l.Page}").Key]).Transform!);
            double lowestOwn = double.PositiveInfinity, highestOffset = 0, highestPainted = 0;
            GridIdentification Check(Layout page, Transform transform, GridSample sample, out double score)
            {
                var evidence = new List<GridCandidateEvidence>();
                var result = GridIdentifier.Identify(layouts, size, sample, l => l == page ? transform : saved[l],
                    buttons: GridFixture.Buttons, observe: evidence.Add);
                score = evidence.SingleOrDefault(e => e.Layout == page)?.Location.Score ?? 0;
                return result;
            }
            foreach (var (page, other) in pairs)
            {
                var layout = layouts.Single(l => $"{l.TabId}/{l.Page}" == page);
                var own = scenes[labels.First(p => p.Value == page).Key];
                foreach (var dx in new[] { -1, 0, 1 })
                foreach (var dy in new[] { -1, 0, 1 })
                {
                    var t = saved[layout] with { X = saved[layout].X + dx, Y = saved[layout].Y + dy };
                    var result = Check(layout, t, scenes[other], out var score);
                    highestOffset = Math.Max(highestOffset, score);
                    if (result.Layout == layout || result.HoverPage == layout) failures.Add($"offset {width}/{page}/{other}/{dx}/{dy}: {score:F2}");
                    var self = Check(layout, t, own, out score);
                    lowestOwn = Math.Min(lowestOwn, score);
                    if (self.Layout != layout) failures.Add($"own offset {width}/{page}/{dx}/{dy}");
                }
            }
            foreach (var layout in layouts)
            {
                var transform = saved[layout];
                var ownKey = labels.First(p => p.Value == $"{layout.TabId}/{layout.Page}").Key;
                var own = scenes[ownKey];
                var world = (byte[])own.Bgra.Clone();
                var right = (int)Math.Ceiling(layout.Cells.Max(c => transform.Apply(Layouts.Origin(layout, c)).X + c.W * transform.Scale) + 8);
                var bottom = (int)Math.Ceiling(layout.Cells.Max(c => transform.Apply(Layouts.Origin(layout, c)).Y + c.H * transform.Scale) + 8);
                for (var y = 0; y < bottom; y++) Buffer.BlockCopy(own.Bgra, (y * width + (int)(width * .42)) * 4, world, y * width * 4, right * 4);
                foreach (var (name, scene) in scenes.Where(p => p.Key != ownKey).Append(new("world", own with { Bgra = world })))
                foreach (var fraction in new[] { 5, 10 })
                {
                    var pixels = (byte[])scene.Bgra.Clone(); var i = 0;
                    foreach (var cell in layout.Cells)
                    {
                        if (i++ % 10 >= fraction) continue;
                        var tb = Geometry.TagBounds(layout, cell, transform);
                        var height = Math.Min(tb.Height, 27 * size.Height / 1080d);
                        var y0 = (int)Math.Round(tb.Y + (tb.Height - height) / 2);
                        for (var y = y0; y < (int)Math.Round(y0 + height); y++)
                        for (var x = (int)Math.Round(tb.X); x < (int)Math.Round(tb.X + tb.Width); x++)
                        for (var c = 0; c < 3; c++) pixels[(y * width + x) * 4 + c] = (byte)(pixels[(y * width + x) * 4 + c] * 85 / 255);
                    }
                    var result = Check(layout, transform, scene with { Bgra = pixels }, out var score);
                    highestPainted = Math.Max(highestPainted, score);
                    if (result.Layout == layout || result.HoverPage == layout)
                    {
                        var fresh = Check(layout, transform, scene, out _);
                        if (fresh.Layout == layout || fresh.HoverPage == layout)
                            failures.Add($"fresh {width}/{layout.TabId}/{layout.Page}/{name}");
                        if (!PaintedCaptureReplay.Hides(result, layout))
                            failures.Add($"unbounded {width}/{layout.TabId}/{layout.Page}/{name}/{fraction}: layout={result.Layout?.Page}, hover={result.HoverPage?.Page}");
                    }
                }
            }
            output.WriteLine($"{width}: own min={lowestOwn:F2}, wrong offset max={highestOffset:F2}, painted max={highestPainted:F2}, gap={lowestOwn - Math.Max(highestOffset, highestPainted):F2}");
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
