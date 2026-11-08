using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class WideSearchBudgetTests
{
    private static double[] Header(double value) => Enumerable.Repeat(value, PageSignature.Length).ToArray();

    [Fact]
    public void GridLikeUnknownSceneGetsOnlyOneWideBatchOverOneHundredTicks()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var page = layouts.First(l => l.TabId == "currency" && l.Page == "general");
        var size = new Resolution(1920, 1080);
        // A regular unpriced grid covering the stash area, deliberately not a shipped page.
        var cells = Enumerable.Range(0, 180).Select(i =>
            new Cell($"grid{i}", 25 + i % 12 * 42, 145 + i / 12 * 42, 39, 39, "Currency", ItemName: "unpriced")).ToArray();
        var scene = page with { Cells = cells };
        var pixels = GridFixture.Render(scene, size, Geometry.Fit(page.ReferenceResolution, size), 1, 1);
        var precheck = GridPrecheck.Score(layouts, size, _ => null, a => GridFixture.Crop(pixels, size, a));
        Assert.Contains(precheck.Values, StashWatchPolicy.ShouldIdentify);
        var policy = new StashWatchPolicy();
        var budget = new WideSearchBudget();
        var batches = 0; var searches = 0;
        for (var i = 0; i < 100; i++)
        {
            var now = DateTimeOffset.UnixEpoch.AddMilliseconds(i * 450);
            if (policy.Tick(now, Header(0), null).Work != WatchWork.Precheck) continue;
            var result = GridIdentifier.Identify(layouts, size, new(0, 0, size.Width, size.Height, pixels),
                _ => null, precheck, candidates => budget.Reserve(now, Header(0), candidates));
            Assert.Null(result.Layout);
            if (result.BroadSearches > 0) batches++;
            searches += result.BroadSearches;
            policy.Unknown(now, false, Header(0));
        }
        Assert.Equal(1, batches);
        Assert.InRange(searches, 1, 2);
    }

    [Fact]
    public void ClosingAndReopeningSameHeaderCreatesNewBaselineButKeepsCooldown()
    {
        var page = GridFixture.Layout; var budget = new WideSearchBudget();
        var start = DateTimeOffset.UnixEpoch;
        Assert.Single(budget.Reserve(start, Header(0), [page]));
        budget.ObserveHeader(Header(.5)); // closed stash
        budget.ObserveHeader(Header(0)); // same page reopened
        Assert.Empty(budget.Reserve(start.AddSeconds(5), Header(0), [page]));
        Assert.Single(budget.Reserve(start.AddSeconds(30), Header(0), [page]));
        Assert.Empty(budget.Reserve(start.AddSeconds(60), Header(0), [page]));
    }

    [Fact]
    public void PartialBatchLeavesBaselineOpenForCoolingCandidateWithBoundedRetries()
    {
        var a = GridFixture.Layout;
        var b = a with { Page = "second" };
        var budget = new WideSearchBudget();
        var start = DateTimeOffset.UnixEpoch;
        Assert.Single(budget.Reserve(start.AddSeconds(-10), Header(-.2), [b]));
        Assert.Single(budget.Reserve(start, Header(0), [a]));
        var attempts = new Dictionary<Layout, List<DateTimeOffset>>
        {
            [a] = [start], [b] = [start.AddSeconds(-10)]
        };
        var partial = budget.Reserve(start.AddSeconds(20), Header(.5), [a, b]);
        Assert.Equal(b, Assert.Single(partial));
        attempts[b].Add(start.AddSeconds(20));
        var resumed = budget.Reserve(start.AddSeconds(30), Header(.5), [a, b]);
        Assert.Equal(a, Assert.Single(resumed));
        attempts[a].Add(start.AddSeconds(30));

        for (var i = 1; i <= 200; i++)
        {
            var now = start.AddSeconds(30).AddMilliseconds(i * 450);
            var batch = budget.Reserve(now, Header(.5), [a, b]);
            Assert.InRange(batch.Count, 0, 2);
            foreach (var page in batch)
            {
                Assert.True(now - attempts[page][^1] >= TimeSpan.FromSeconds(30));
                attempts[page].Add(now);
            }
            foreach (var history in attempts.Values)
                Assert.True(history.Count <= Math.Ceiling((now - history[0]).TotalSeconds / 30) + 1);
        }
    }

    [Fact]
    public void FullyAvailableBatchConsumesBaselineAndNeverExceedsTwoCandidates()
    {
        var a = GridFixture.Layout;
        var b = a with { Page = "second" };
        var c = a with { Page = "third" };
        var budget = new WideSearchBudget();
        var start = DateTimeOffset.UnixEpoch;
        var batch = budget.Reserve(start, Header(0), [a, b, c]);
        Assert.Equal(2, batch.Count);
        Assert.Contains(a, batch); Assert.Contains(b, batch); Assert.DoesNotContain(c, batch);
        Assert.Empty(budget.Reserve(start.AddMinutes(1), Header(0), [a, b, c]));
    }

    [Fact]
    public void JitterCannotBypassThirtySecondPerLayoutCooldown()
    {
        var page = GridFixture.Layout;
        var budget = new WideSearchBudget();
        var attempts = new List<DateTimeOffset>();
        for (var i = 0; i < 100; i++)
        {
            var now = DateTimeOffset.UnixEpoch.AddMilliseconds(i * 450);
            if (budget.Reserve(now, Header(i % 2 == 0 ? .2 : -.2), [page]).Contains(page))
                attempts.Add(now);
        }
        Assert.Equal(2, attempts.Count);
        Assert.True(attempts[1] - attempts[0] >= TimeSpan.FromSeconds(30));
        // Jitter since the last attempt created a new baseline; allow one after cooldown.
        Assert.Single(budget.Reserve(attempts[^1].AddMinutes(1), Header(-.2), [page]));
        Assert.Empty(budget.Reserve(attempts[^1].AddMinutes(2), Header(-.2), [page]));
    }
}
