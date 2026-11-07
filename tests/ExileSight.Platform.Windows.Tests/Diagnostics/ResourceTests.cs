using System.Diagnostics;
using ExileSight.Platform.Windows.Diagnostics;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Diagnostics;

public sealed class ResourceTests
{
    [WindowsOnlyFact]
    public void ReadsOwnPrivateMemoryAndMonotonicCpu()
    {
        var before = OwnProcessResources.Read();
        var after = OwnProcessResources.Read();
        Assert.True(before.PrivateBytes > 0);
        Assert.True(after.CpuTime >= before.CpuTime);
        Assert.True(after.Timestamp >= before.Timestamp);
    }

    [Fact]
    public void CpuBudgetIsOneCoreAndNotMachineNormalized()
    {
        var before = new ResourceSample(0, TimeSpan.Zero, 0);
        var after = new ResourceSample(60 * Stopwatch.Frequency, TimeSpan.FromSeconds(.6), 0);
        Assert.Equal(1d, OwnProcessResources.CpuPercent(before, after), 8);
    }
}
