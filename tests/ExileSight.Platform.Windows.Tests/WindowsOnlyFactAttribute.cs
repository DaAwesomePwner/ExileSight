using Xunit;

namespace ExileSight.Platform.Windows.Tests;

public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Requires Windows; exercised by Windows CI.";
    }
}
