using Xunit;

namespace ExileSight.Perf.Tests;

public sealed class PublishedAppFactAttribute : FactAttribute
{
    public PublishedAppFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires Windows; exercised by performance CI.";
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EXILESIGHT_PERF_APP")))
            Skip = "Set EXILESIGHT_PERF_APP to the published executable; performance CI requires this.";
    }
}
