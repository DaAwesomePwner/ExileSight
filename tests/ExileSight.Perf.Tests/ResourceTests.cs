using System.Diagnostics;
using ExileSight.Platform.Windows.Diagnostics;
using Xunit;

namespace ExileSight.Perf.Tests;

public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires Windows; exercised by Windows CI.";
    }
}

public sealed class PublishedAppFactAttribute : FactAttribute
{
    public PublishedAppFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires Windows; exercised by performance CI.";
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EXILESIGHT_PERF_APP")))
            Skip = "Set EXILESIGHT_PERF_APP to the published executable; performance CI requires this.";
    }
}

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
