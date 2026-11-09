using ExileSight.Features.Guide;
using Xunit;

namespace ExileSight.Features.Guide.Tests;

public sealed class GuidePageStateTests
{
    private static GuideSnapshot Snapshot() => new(true, 2, 3, 9, TimeSpan.Zero, TimeSpan.Zero,
        1d / 3, ["Find the waypoint"], "Return to town", null, false);

    [Fact]
    public void EqualContentIgnoresNewArraysAndUnshownTimers()
    {
        Assert.True(GuidePageState.SameContent(Snapshot(), Snapshot()));
        Assert.True(GuidePageState.SameContent(Snapshot(), Snapshot() with
            { ActTime = TimeSpan.FromMinutes(1), TotalTime = TimeSpan.FromMinutes(2), Visible = false }));
        Assert.True(GuidePageState.SameContent(null, null));
        Assert.False(GuidePageState.SameContent(null, Snapshot()));
    }

    [Fact]
    public void PageChangesInvalidateContent()
    {
        var original = Snapshot();
        GuideSnapshot[] changes = [original with { Act = 3 }, original with { Step = 4 },
            original with { StepCount = 10 }, original with { LeagueStart = false },
            original with { Bandit = "oak" }, original with { HasCustomGuide = true },
            original with { Notice = "Progress and timers reset." }, original with { Complete = true },
            original with { Lines = ["A different instruction"] }, original with { NextLine = "Next area" }];
        Assert.All(changes, changed => Assert.False(GuidePageState.SameContent(original, changed)));
    }

    [Fact]
    public void ModelNoticeSurvivesRepeatedSnapshots()
    {
        var model = new GuideModel(); model.Load(GuideTests.Data(), "fixture"); model.Reset();
        var first = model.StructuredSnapshot();
        Assert.Equal("Progress and timers reset.", first.Notice);
        Assert.True(GuidePageState.SameContent(first, model.StructuredSnapshot()));
    }
}
