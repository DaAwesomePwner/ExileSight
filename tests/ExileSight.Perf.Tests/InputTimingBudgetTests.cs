using ExileSight.Core.Input;
using Xunit;

namespace ExileSight.Perf.Tests;

public sealed class InputTimingBudgetTests
{
    [Fact]
    public void RecordingDoesNotAllocatePerEventOrGrowStorage()
    {
        var ring = new InputTiming();
        var entry = new InputTimingEntry(1, InputStage.HookDelay, 0, InputTimingOutcome.Observed);
        for (var i = 0; i < 100; i++) ring.Add(entry);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++) ring.Add(entry);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(InputTiming.Capacity, ring.Snapshot().Length);
    }
}
