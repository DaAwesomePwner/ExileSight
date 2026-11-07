using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class RepeatThresholdTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoSecondDelayAndRepeatsStayHeldWithPlatformThreshold(bool feature)
    {
        var dispatcher = Create(() => 2200, feature);
        Assert.NotNull(dispatcher.Observe(Key(10000), true).Press);
        for (var timestamp = 12000; timestamp <= 30000; timestamp += 2000)
        {
            var repeat = dispatcher.Observe(Key(timestamp), true);
            Assert.Null(repeat.Press);
            Assert.True(repeat.Block);
        }
        Assert.True(dispatcher.Observe(Key(30001) with { Down = false }, true).Block);
        Assert.NotNull(dispatcher.Observe(Key(30002), true).Press);
    }

    [Fact]
    public void UpdatedThresholdIsReadOnNextDownWithoutRecreatingDispatcher()
    {
        long threshold = 1200;
        var dispatcher = Create(() => Volatile.Read(ref threshold));
        Assert.NotNull(dispatcher.Observe(Key(10000), true).Press);
        Volatile.Write(ref threshold, 2200);
        Assert.Null(dispatcher.Observe(Key(12000), true).Press);
        Assert.Null(dispatcher.Observe(Key(14000), true).Press);
        Volatile.Write(ref threshold, 4200); // acceptance delay also increases the gap
        Assert.Null(dispatcher.Observe(Key(18000), true).Press);
        Volatile.Write(ref threshold, 1200);
        Assert.NotNull(dispatcher.Observe(Key(19201), true).Press);
    }

    [Fact]
    public void OmittedProviderKeepsTwelveHundredMillisecondDefault()
    {
        var dispatcher = Create();
        Assert.Equal(1200, HotkeyDispatcher.BlockedReleaseSilenceMilliseconds);
        Assert.NotNull(dispatcher.Observe(Key(10000), true).Press);
        Assert.Null(dispatcher.Observe(Key(11200), true).Press);
        Assert.NotNull(dispatcher.Observe(Key(12401), true).Press);
    }

    [Fact]
    public void FailedPlatformReadCanDisableSilenceRecovery()
    {
        var dispatcher = Create(() => long.MaxValue);
        Assert.NotNull(dispatcher.Observe(Key(10000), true).Press);
        Assert.Null(dispatcher.Observe(Key(70000), true).Press);
        Assert.True(dispatcher.Observe(Key(70001) with { Down = false }, true).Block);
        Assert.NotNull(dispatcher.Observe(Key(70002), true).Press);
    }

    private static HotkeyDispatcher Create(Func<long>? threshold = null, bool feature = false) =>
        new(TestIntents.Create(new FakeClock()), [new("binding", 118, 0, true,
            feature ? null : new GameAction.CopyItem(), FeatureCommand: feature ? "toggle" : null)], threshold);
    private static PhysicalKey Key(long timestamp) => new(118, true, false, 0, timestamp);
}
