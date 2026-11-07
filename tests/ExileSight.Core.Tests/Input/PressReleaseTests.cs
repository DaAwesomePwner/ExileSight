using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class PressReleaseTests
{
    private static HotkeyDispatcher Create(TestClock clock) => new(TestIntents.Create(clock),
        [new("omni", 0x14, 0, true, new GameAction.CopyItem(), TrackRelease: true)]);
    private static PhysicalKey Key(long timestamp, bool down = true, bool injected = false) =>
        new(0x14, down, injected, 0, timestamp);
    [Fact]
    public void ReleasePreservesHookDurationAndNeverIssuesAnotherToken()
    {
        var clock = new TestClock();
        var dispatcher = Create(clock);
        TestIntents.Authorize(dispatcher);
        var tokens = 0;
        PressReleased? released = null;
        var router = new PressRouter(dispatcher, clock, () => true, (token, _) =>
        { Assert.NotNull(token); tokens++; return new(ActionOutcome.Success); },
            release: (_, value) => released = value);
        var down = dispatcher.Observe(Key(clock.Milliseconds), true);
        Assert.True(down.Block);
        Assert.Equal(ActionOutcome.Success, router.Route(down.Press!).Outcome);
        Assert.Null(dispatcher.Observe(Key(clock.Milliseconds + 50), true).Press);
        var up = dispatcher.Observe(Key(clock.Milliseconds + 201, false), true);
        Assert.True(up.Block);
        Assert.Throws<InvalidOperationException>(() => dispatcher.Issue(up.Press!));
        clock.Delay(5000); // worker delay must not affect classification or expire the release
        Assert.Equal(ActionOutcome.Success, router.Route(up.Press!).Outcome);
        Assert.Equal(new PressReleased(down.Press!.Id, 10000, 10201), released);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, router.Route(up.Press!).Outcome);
        Assert.Equal(1, tokens);
        Assert.Null(dispatcher.Observe(Key(10202, false), true).Press);
    }
    [Fact]
    public void LostReleaseIsDroppedWhenSilenceRecoveryStartsAnotherPress()
    {
        var clock = new TestClock();
        var dispatcher = Create(clock);
        var first = dispatcher.Observe(Key(10000), true).Press!;
        var next = dispatcher.Observe(Key(12001), true).Press!;
        Assert.NotEqual(first.Id, next.Id);
        var up = dispatcher.Observe(Key(12100, false), true);
        Assert.Same(next, up.Press!.Origin);
        Assert.True(up.Block);
    }
    [Fact]
    public void InjectedReleaseDoesNotEndPhysicalPress()
    {
        var dispatcher = Create(new());
        var first = dispatcher.Observe(Key(10000), true).Press!;
        Assert.Null(dispatcher.Observe(Key(10100, false, true), true).Press);
        Assert.Same(first, dispatcher.Observe(Key(10200, false), true).Press!.Origin);
    }
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void MissingDownOrLostFocusCannotDeliverRelease(bool executeDown, bool focused)
    {
        var clock = new TestClock();
        var dispatcher = Create(clock);
        TestIntents.Authorize(dispatcher);
        var calls = 0;
        var focus = true;
        var router = new PressRouter(dispatcher, clock, () => focus, (_, _) => new(ActionOutcome.Success),
            release: (_, _) => calls++);
        var down = dispatcher.Observe(Key(10000), true);
        if (executeDown) router.Route(down.Press!);
        focus = focused;
        router.Route(dispatcher.Observe(Key(10300, false), focused).Press!);
        Assert.Equal(0, calls);
    }
    [Fact]
    public void PassThroughClearsReleaseTrackingWithoutChangingExistingBlockPolicy()
    {
        var dispatcher = Create(new());
        dispatcher.Observe(Key(10000), true);
        var up = dispatcher.Observe(Key(10100, false), true, passThrough: true);
        Assert.False(up.Block);
        Assert.Null(up.Press);
    }
}
