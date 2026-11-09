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
        var failures = new List<string>(); var cases = 0;
        var searches = new Dictionary<(string Name, int Width), List<int>>();
        double minimumCorrect = double.PositiveInfinity, maximumAdmittedWrong = 0, maximumAdmittedNull = 0, minimumMargin = double.PositiveInfinity;
        var worstWrong = "none";
        foreach (var (name, expected) in labels.Where(p => p.Key.StartsWith("src", StringComparison.Ordinal)))
        foreach (var width in new[] { 2000, 3440 })
        foreach (var shiftX in new[] { 0, -5, 5 })
        foreach (var shiftY in new[] { 0, -5, 5 })
        {
            cases++;
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
            var admitted = new HashSet<Layout>(); var evidence = new List<GridCandidateEvidence>();
            // Replay the production first-use path: real sparse crops, no saved
            // calibration, and a budget that grants every proposed wide candidate.
            var result = GridIdentifier.Identify(layouts, size, sample, _ => null, pre,
                reserveWideSearch: candidates => { admitted.UnionWith(candidates); return candidates.ToHashSet(); },
                buttons: GridFixture.Buttons, observe: evidence.Add);
            if (!searches.TryGetValue((name, width), out var counts)) searches[(name, width)] = counts = [];
            counts.Add(result.BroadSearches);
            foreach (var candidate in evidence.Where(c => c.Wide && $"{c.Layout.TabId}/{c.Layout.Page}" != expected))
            {
                if (expected is null) maximumAdmittedNull = Math.Max(maximumAdmittedNull, candidate.Location.Score);
                if (candidate.Location.Score <= maximumAdmittedWrong) continue;
                maximumAdmittedWrong = candidate.Location.Score;
                var otherLocal = evidence.Where(c => !c.Wide && c.Layout != candidate.Layout).Select(c => c.Location.Score).DefaultIfEmpty(0).Max();
                var wideGate = GridWideAcceptance.Accepts(candidate.Location, Geometry.Fit(candidate.Layout.ReferenceResolution, size), otherLocal);
                worstWrong = $"{name}/{width}/({shiftX},{shiftY}) => {candidate.Layout.TabId}/{candidate.Layout.Page}, wide gate={wideGate}, other local={otherLocal:F2}";
            }
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
            output.WriteLine($"{name} {width} shift=({shiftX},{shiftY}): {expected ?? "Unknown"} => {actual ?? "Unknown"}, score={result.Score:F2}, margin={result.Margin:F2}, pre={(layout is null ? pre.Values.Max() : pre[layout]):F2}, error={error:F2}px, wide={result.BroadSearches}, {elapsed:F1}ms");
            var tag = $"{name}/{width}/({shiftX},{shiftY})";
            if (admitted.Count != result.BroadSearches || result.BroadSearches > 2 ||
                evidence.Count(c => c.Wide) != result.BroadSearches || !admitted.SetEquals(evidence.Where(c => c.Wide).Select(c => c.Layout)))
                failures.Add($"{tag}: wide reservation/execution mismatch");
            if (expected == actual && expected is not null)
            {
                minimumCorrect = Math.Min(minimumCorrect, result.Score);
                minimumMargin = Math.Min(minimumMargin, result.Margin);
            }
            if (expected != actual) failures.Add($"{tag}: expected {expected ?? "Unknown"}, got {actual ?? "Unknown"}");
            if (layout is null) continue;
            if (result.Margin < GridIdentifier.MinimumMargin) failures.Add($"{tag}: margin {result.Margin:F2}");
            if (!StashWatchPolicy.ShouldIdentify(pre[layout])) failures.Add($"{tag}: precheck {pre[layout]:F2}");
            if (wide!.Transform is null || error > (width == 2000 ? 2 : 3)) failures.Add($"{tag}: wide error {error:F2}");
        }
        foreach (var width in new[] { 2000, 3440 })
        {
            var bytes = File.ReadAllBytes(Path.Combine(folder, "bmp", $"src6-{width}.bmp"));
            var size = new Resolution(BitConverter.ToInt32(bytes, 18), -BitConverter.ToInt32(bytes, 22));
            var pixels = bytes.AsSpan(BitConverter.ToInt32(bytes, 10), size.Width * size.Height * 4).ToArray();
            GridFixture.BrightenButton(pixels, size, "league", 25);
            var pre = GridPrecheck.Score(layouts, size, _ => null, a => GridFixture.Crop(pixels, size, a));
            var result = GridIdentifier.Identify(layouts, size, new(0, 0, size.Width, size.Height, pixels), _ => null, pre,
                reserveWideSearch: candidates => candidates.ToHashSet(), buttons: GridFixture.Buttons);
            output.WriteLine($"Atlas +25 League hover {width}: {result.Layout?.Page ?? "Unknown"}, score={result.Score:F2}, margin={result.Margin:F2}");
            if (result.Layout is not null && result.Layout.Page != "atlas") failures.Add($"Atlas hover/{width}: {result.Layout.Page}");
        }
        foreach (var (key, counts) in searches)
            output.WriteLine($"PRODUCTION {key.Name}/{key.Width}: wide per shift=[{string.Join(",", counts)}], total={counts.Sum()}, max={counts.Max()}");
        output.WriteLine($"PRODUCTION gap: lowest correct={minimumCorrect:F2}, highest admitted wrong/null candidate={maximumAdmittedWrong:F2} ({worstWrong}), gap={minimumCorrect - maximumAdmittedWrong:F2}, highest admitted null={maximumAdmittedNull:F2}, null gap={minimumCorrect - maximumAdmittedNull:F2}, minimum decision margin={minimumMargin:F2}");
        output.WriteLine($"Checked {cases} image/shift cases and two Atlas hover-glow cases; {failures.Count} failures.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
