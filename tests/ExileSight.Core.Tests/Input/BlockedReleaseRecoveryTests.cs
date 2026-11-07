using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class BlockedReleaseRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BlockedRepeatsAcrossContextChangesNeverConsultAsyncStateOrIssueAgain(bool feature)
    {
        var configuration = new HotkeyConfiguration();
        configuration.UpdateGameFocused(true, 1);
        configuration.Register("overlay");
        configuration.Replace([new("binding", 118, 0, true,
            feature ? null : new GameAction.CopyItem(), FeatureCommand: feature ? "toggle" : null)], []);
        var dispatcher = new HotkeyDispatcher(new(new FakeClock()), configuration);
        Assert.NotNull(dispatcher.Observe(Key(10000), true).Press);

        // The feature opens an overlay; Windows reports a blocked key as up.
        configuration.SetActive("overlay", true);
        dispatcher.ReconcileHeld(_ => throw new InvalidOperationException("Blocked async state is not evidence"));
        var asyncReads = 0;
        for (var timestamp = 11000; timestamp <= 20000; timestamp += 100)
        {
            configuration.UpdateGameFocused(timestamp % 200 == 0, timestamp % 200 == 0 ? 1 : 2);
            dispatcher.ReconcileHeld(_ => { asyncReads++; return false; });
            var repeat = dispatcher.Observe(Key(timestamp), true);
            Assert.Null(repeat.Press);
            Assert.True(repeat.Block);
        }
        Assert.Equal(0, asyncReads);
        Assert.True(dispatcher.Observe(Key(20001) with { Down = false }, true).Block);
        Assert.False(dispatcher.Observe(Key(20002) with { Down = false }, true).Block);
        configuration.UpdateGameFocused(true, 1);
        Assert.NotNull(dispatcher.Observe(Key(20003), true).Press);
    }

    [Fact]
    public void LostBlockedReleaseRecoversOnlyAfterSilenceSinceMostRecentRepeat()
    {
        var dispatcher = Create(block: true);
        Assert.NotNull(dispatcher.Observe(Key(10000), true).Press);
        // The longest normal initial repeat delay remains suppressed.
        Assert.Null(dispatcher.Observe(Key(11000), true).Press);
        Assert.Null(dispatcher.Observe(Key(12200), true).Press); // exact boundary refreshes the interval
        // No release arrives. A later physical down is accepted without a context change.
        Assert.NotNull(dispatcher.Observe(Key(13401), true).Press);
        Assert.Null(dispatcher.Observe(Key(13402), true).Press);
        Assert.True(dispatcher.Observe(Key(13403) with { Down = false }, true).Block);
    }

    [Fact]
    public void InjectedDownDoesNotExtendRecoveryInterval()
    {
        var dispatcher = Create(block: true);
        Assert.NotNull(dispatcher.Observe(Key(10000), true).Press);
        Assert.Equal(default, dispatcher.Observe(Key(11100) with { Injected = true }, true));
        Assert.NotNull(dispatcher.Observe(Key(11201), true).Press);
    }

    [Fact]
    public void PassThroughKeysStillReconcileUsingAsyncStateAndDoNotUseSilenceRecovery()
    {
        var configuration = new HotkeyConfiguration();
        configuration.UpdateGameFocused(true, 1);
        configuration.Replace([new("copy", 118, 0, false, new GameAction.CopyItem())], []);
        var dispatcher = new HotkeyDispatcher(new(new FakeClock()), configuration);
        Assert.NotNull(dispatcher.Observe(Key(10000), true).Press);
        Assert.Null(dispatcher.Observe(Key(15000), true).Press);
        configuration.UpdateGameFocused(true, 2);
        dispatcher.ReconcileHeld(_ => true);
        Assert.Null(dispatcher.Observe(Key(15001), true).Press);
        configuration.UpdateGameFocused(true, 3);
        dispatcher.ReconcileHeld(_ => false);
        var next = dispatcher.Observe(Key(15002), true);
        Assert.NotNull(next.Press);
        Assert.False(next.Block);
        Assert.False(dispatcher.Observe(Key(15003) with { Down = false }, true).Block);
    }

    private static HotkeyDispatcher Create(bool block) =>
        new(new(new FakeClock()), [new("copy", 118, 0, block, new GameAction.CopyItem())]);
    private static PhysicalKey Key(long timestamp) => new(118, true, false, 0, timestamp);
}
