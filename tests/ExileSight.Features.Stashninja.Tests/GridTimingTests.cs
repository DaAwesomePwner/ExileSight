using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.Stashninja.Tests;

[CollectionDefinition("Grid timing", DisableParallelization = true)]
public sealed class GridTimingCollection;

[Collection("Grid timing")]
public sealed class GridTimingTests(ITestOutputHelper output)
{
    [Fact]
    public void ReleaseLocalAndWideSearchMeetBudgets()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var page = layouts.Single(l => l.TabId == "currency" && l.Page == "general");
        var size = new Resolution(3440, 1440);
        var fit = Geometry.Fit(page.ReferenceResolution, size);
        var sample = new GridSample(0, 0, size.Width, size.Height, GridFixture.Render(page, size, fit, 1, 1));
        var buttons = GridFixture.Buttons;
        GridIdentification Local() => GridIdentifier.Identify(layouts, size, sample, _ => null,
            reserveWideSearch: _ => new HashSet<Layout>(), buttons: buttons);
        Local(); GridLocator.Locate(page, size, fit, sample); // JIT warm-up outside timings.
        var watch = Stopwatch.StartNew();
        var result = Local();
        var localMs = watch.Elapsed.TotalMilliseconds;
        Assert.Equal(page, result.Layout);
        Assert.Equal(0, result.BroadSearches);
        Assert.InRange(result.Evaluations, 1, layouts.Length * 559);
        Assert.InRange(result.ScorePasses, result.Evaluations, 2 * result.Evaluations + layouts.Length * 16);
        Assert.InRange(result.ScoreSampleReads, 1, result.ScorePasses * 24 * 12 * 5 * 4);
        watch.Restart();
        var wide = GridLocator.Locate(page, size, fit, sample);
        var wideMs = watch.Elapsed.TotalMilliseconds;
        Assert.NotNull(wide.Transform);
        Assert.InRange(wide.Evaluations, 1, GridLocator.MaxEvaluations);
        Assert.InRange(wide.ScorePasses, wide.Evaluations, 2 * wide.Evaluations + 16);
        Assert.InRange(wide.ScoreSampleReads, 1, wide.ScorePasses * 24 * 12 * 5 * 4);
        output.WriteLine($"Local all-eight: {localMs:F1} ms; wide: {wideMs:F1} ms; capture excluded");
    }
}
