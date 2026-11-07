using ExileSight.Core.Logs;
using Xunit;
namespace ExileSight.Features.MapTracker.Tests;

public sealed class SilenceTests
{
    [Theory]
    [InlineData(240, false)]
    [InlineData(1800, false)]
    [InlineData(1800, true)]
    public async Task LaterLogCreditsFullGapAndKeepsOneRunAndConfirmedDeaths(int seconds, bool replay)
    {
        var store = new TestStore(); var model = new TrackerModel(store);
        model.SelectLeague("L");
        model.Accept(IdentityTests.Level(0, "Self") with { IsReplay = replay });
        Assert.True(model.ConfirmCharacter("Self"));
        model.Accept(TransitionTests.Area(0, "MapWorldsBeach") with { IsReplay = replay });
        await model.FlushAsync(); var first = Assert.Single(store.Read("L"));
        model.Accept(TransitionTests.E(new PlayerSlain("", TransitionTests.At(seconds), "Self")) with { IsReplay = replay });
        model.Accept(TransitionTests.Area(seconds, "MapWorldsBeach") with { IsReplay = replay });
        await model.FlushAsync();
        var run = Assert.Single(store.Read("L"));
        Assert.Equal(first.Id, run.Id); Assert.Equal(seconds, run.Seconds);
        Assert.Equal(1, run.Portals); Assert.Equal(1, run.Deaths); Assert.True(run.DeathsKnown);
        Assert.Contains("Deaths: counting for Self", model.Status);
        model.Accept(TransitionTests.Area(seconds + 10, "MapWorldsDunes") with { IsReplay = replay });
        await model.StopAsync();
        var completed = Assert.Single(store.Read("L"), r => r.Id == first.Id);
        Assert.Equal(seconds + 10, completed.Seconds); Assert.False(completed.Incomplete);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnconfirmKeepsLastConfirmedNameEvenAfterSessionBoundary(bool boundary)
    {
        var tracker = new RunTracker(); tracker.Accept(IdentityTests.Level(0, "Self"), "L");
        Assert.True(tracker.ConfirmCharacter("Self"));
        tracker.Accept(TransitionTests.Area(0, "MapWorldsBeach"), "L");
        if (boundary) tracker.Accept(TransitionTests.E(new LogSourceChanged()), "L");
        Assert.True(tracker.ConfirmCharacter(null));
        Assert.True(tracker.ConfirmCharacter(null));
        Assert.False(tracker.CharacterConfirmed); Assert.Equal("Self", tracker.Reconfirm);
        Assert.Contains("Self", tracker.Candidates);
        if (!boundary) Assert.False(tracker.Current!.DeathsKnown);
        Assert.True(tracker.ConfirmCharacter("Self")); Assert.Null(tracker.Reconfirm);
    }
}
