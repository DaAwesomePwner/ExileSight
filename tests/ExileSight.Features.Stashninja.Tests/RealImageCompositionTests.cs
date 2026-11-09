using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class RealImageCompositionTests(ITestOutputHelper output)
{
    [RealImageFact]
    public void RealImagePaintedFramesAtProductionScalesAreBounded()
    {
        var folder = Environment.GetEnvironmentVariable("EXILESIGHT_REAL_STASH_DIR")!;
        var labels = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(Path.Combine(folder, "labels.json")))!;
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var kept = 0; var total = 0; var failures = new List<string>();
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
            foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
            foreach (var layout in layouts)
            {
                var t = saved[layout];
                var ownKey = labels.First(p => p.Value == $"{layout.TabId}/{layout.Page}").Key;
                var own = scenes[ownKey];
                var clean = GridIdentifier.Identify(layouts, size, own, l => saved[l], buttons: GridFixture.Buttons);
                if (clean.Layout != layout) failures.Add($"clean {width}/{scale}/{layout.TabId}/{layout.Page}");
                var freshResults = new Dictionary<string, GridIdentification>();
                var world = (byte[])own.Bgra.Clone();
                var right = (int)Math.Ceiling(layout.Cells.Max(c => t.Apply(Layouts.Origin(layout, c)).X + c.W * t.Scale) + 8);
                var bottom = (int)Math.Ceiling(layout.Cells.Max(c => t.Apply(Layouts.Origin(layout, c)).Y + c.H * t.Scale) + 8);
                for (var y = 0; y < bottom; y++) Buffer.BlockCopy(own.Bgra, (y * width + (int)(width * .42)) * 4, world, y * width * 4, right * 4);
                foreach (var (name, scene) in scenes.Where(p => p.Key != ownKey).Append(new("world", own with { Bgra = world })))
                foreach (var dx in new[] { -1, 0, 1 })
                {
                    var pixels = (byte[])scene.Bgra.Clone();
                    foreach (var cell in layout.Cells)
                    {
                        var b = Geometry.TagBounds(layout, cell, t);
                        var h = Math.Min(b.Height, 27 * scale);
                        var y0 = (int)Math.Round(b.Y + (b.Height - h) / 2) + dx;
                        for (var y = y0; y < (int)Math.Round(y0 + h); y++)
                        for (var x = (int)Math.Round(b.X); x < (int)Math.Round(b.X + b.Width); x++)
                        for (var c = 0; c < 3; c++) pixels[(y * width + x) * 4 + c] = (byte)(pixels[(y * width + x) * 4 + c] * 85 / 255);
                    }
                    var s = scene with { Bgra = pixels };
                    var result = GridIdentifier.Identify(layouts, size, s, l => saved[l], buttons: GridFixture.Buttons);
                    total++;
                    if (result.Layout == layout || result.HoverPage == layout)
                    {
                        kept++;
                        if (!freshResults.TryGetValue(name, out var fresh))
                            freshResults[name] = fresh = GridIdentifier.Identify(layouts, size, scene, l => saved[l], buttons: GridFixture.Buttons);
                        if (fresh.Layout == layout || fresh.HoverPage == layout)
                            failures.Add($"fresh {width}/{scale}/{layout.TabId}/{layout.Page}/{name}");
                        if (!PaintedCaptureReplay.Hides(result, layout))
                            failures.Add($"unbounded {width}/{scale}/{layout.TabId}/{layout.Page}/{name}/dy={dx}: normal={result.Layout?.Page}, hover={result.HoverPage?.Page}");
                    }
                }
            }
        }
        output.WriteLine($"production painted frames={total}, transient keeps={kept}, unbounded={failures.Count}");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
