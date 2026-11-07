using Xunit;

namespace ExileSight.Platform.Windows.Tests;

public sealed class PlatformSmokeTests
{
    [WindowsOnlyFact]
    public void PlatformAssemblyLoads() => Assert.Equal("ExileSight.Platform.Windows",
        System.Reflection.Assembly.Load("ExileSight.Platform.Windows").GetName().Name);
}
