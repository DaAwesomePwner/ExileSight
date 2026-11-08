using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class ColdPressTests
{
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
