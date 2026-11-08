using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class ColdPressTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(201)]
    public void UnrelatedPassThroughAndStaleReleasePreservePhysicalDuration(long duration)
    {
        var clock = new TestClock();
        var dispatcher = new HotkeyDispatcher(TestIntents.Create(clock),
            [new("omni", 0x14, 0, true, new GameAction.CopyItem(), TrackRelease: true)]);
        TestIntents.Authorize(dispatcher);
        var copies = 0;
        PressReleased? released = null;
        var router = new PressRouter(dispatcher, clock, () => true,
            (_, _) => { copies++; return new(ActionOutcome.Success); }, release: (_, value) => released = value);
        var down = dispatcher.Observe(new(0x14, true, false, 0, 10000), true).Press!;
        router.Route(down);
        var other = dispatcher.Observe(new(65, true, false, 0, 10050), true, true);
        Assert.False(other.Block);
        Assert.Null(other.Press);
        clock.Delay(600); // stale lease/cold worker, but native duration remains authoritative
        var up = dispatcher.Observe(new(0x14, false, false, 0, 10000 + duration), true, true);
        Assert.True(up.Block);
        Assert.Equal(ActionOutcome.Success, router.Route(up.Press!).Outcome);
        Assert.Equal(new PressReleased(down.Id, 10000, 10000 + duration), released);
        Assert.Equal(1, copies);
    }
    [Fact]
    public void StaleMacroReleaseRunsDeferredActionOnceWithOriginalToken()
    {
        var clock = new TestClock();
        var dispatcher = new HotkeyDispatcher(TestIntents.Create(clock),
            [new("macro", 0x74, 0, true, new GameAction.SendChatCommand("/hideout"), TrackRelease: true)]);
        TestIntents.Authorize(dispatcher);
        IntentToken? original = null;
        var actions = 0;
        var router = new PressRouter(dispatcher, clock, () => true, (token, _) =>
        {
            original = token;
            return PressRouter.DeferUntilRelease(() => new(ActionOutcome.Success));
        }, release: (_, _) => { Assert.NotNull(original); actions++; });
        router.Route(dispatcher.Observe(new(0x74, true, false, 0, 10000), true).Press!);
        clock.Delay(300);
        var release = dispatcher.Observe(new(0x74, false, false, 0, 10100), true, true);
        Assert.True(release.Block);
        Assert.Equal(ActionOutcome.Success, router.Route(release.Press!).Outcome);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, router.Route(release.Press!).Outcome);
        Assert.Equal(1, actions);
    }
    [Fact]
    public void DiagnosticsAreBoundedOrderedAndContainOnlyNumericFields()
    {
        var ring = new InputTiming();
        for (var i = 0; i < 1000; i++) ring.Add(new(i, InputStage.HookDelay, i, InputTimingOutcome.Observed));
        var entries = ring.Snapshot();
        Assert.Equal(InputTiming.Capacity, entries.Length);
        Assert.Equal(872, entries[0].Timestamp);
        Assert.Equal(999, entries[^1].Timestamp);
        Assert.All(typeof(InputTimingEntry).GetProperties(), p => Assert.True(p.PropertyType.IsValueType));
    }
    [Fact]
    public void ClipboardStageTimeoutIsDistinctAndDoesNotCapturePayload()
    {
        Assert.Throws<TimeoutException>(() => InputTiming.Measure<int>(InputStage.ClipboardSnapshot,
            () => throw new TimeoutException("private payload")));
        Assert.Contains(InputTiming.Shared.Snapshot(), e => e.Stage == InputStage.ClipboardSnapshot &&
            e.Outcome == InputTimingOutcome.Timeout);
    }
}
