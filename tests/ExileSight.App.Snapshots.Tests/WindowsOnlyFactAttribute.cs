using Xunit;

namespace ExileSight.App.Snapshots.Tests;

public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Requires Windows; exercised by Windows CI.";
#if !SNAPSHOT_WINDOWS
        else
            throw new InvalidOperationException("Windows snapshot tests must build with SnapshotRenderingEnabled=true.");
#endif
    }
}
