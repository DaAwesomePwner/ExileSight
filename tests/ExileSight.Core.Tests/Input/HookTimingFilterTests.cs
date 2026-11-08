using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class HookTimingFilterTests
{
    private static HotkeyDispatcher Create() => new(new IntentRegistry(new TestClock()),
        [new("copy", 0x14, 0, true, new GameAction.CopyItem(), TrackRelease: true)]);
    [Fact]
    public void UnrelatedTypingAndRepeatsDoNotEnterOrEvictRing()
    {
        var ring = new InputTiming();
        var dispatcher = Create();
        ring.Add(new(1, InputStage.ClipboardSnapshot, 10, InputTimingOutcome.Timeout));
        for (var i = 0; i < 1000; i++)
        {
            var key = new PhysicalKey(65, i % 3 != 0, false, 0, i);
            ring.RecordHook(dispatcher, key, i, 0, false, false);
            dispatcher.Observe(key, true, true);
        }
        Assert.Equal(InputStage.ClipboardSnapshot, Assert.Single(ring.Snapshot()).Stage);
    }
    [Fact]
    public void ClaimedReleaseIsRecordedEvenAfterContextDisablesBinding()
    {
        var ring = new InputTiming();
        var dispatcher = Create();
        dispatcher.Observe(new(0x14, true, false, 0, 100), true);
        dispatcher.Configuration.UpdateGameFocused(false);
        ring.RecordHook(dispatcher, new(0x14, false, false, 0, 150), 151, 51, false, false);
        Assert.Equal(new[] { InputStage.HookDelay, InputStage.LeaseAge }, ring.Snapshot().Select(e => e.Stage));
    }
    [Theory]
    [InlineData(0x14, 0, false, 2)]
    [InlineData(65, 49, false, 0)]
    [InlineData(65, 50, false, 2)]
    [InlineData(65, 0, true, 3)]
    public void BoundKeysSlowCallbacksAndReevaluationsRemainVisible(int key, int delay, bool reevaluated, int count)
    {
        var ring = new InputTiming();
        ring.RecordHook(Create(), new(key, true, false, 0, 100), 100 + delay, 300, reevaluated, true);
        Assert.Equal(count, ring.Snapshot().Length);
    }
}
