using ExileSight.Core.Features;
using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class DefaultBindingTests
{
    private static readonly FeatureDefaultBinding Default = new("omni.press", 0x14, 0, true, "omni:press");
    [Fact]
    public void MissingDefaultsAreAddedWithoutMutatingPersistedSettings()
    {
        var stored = new List<PersistedBinding>();
        var result = FeatureDefaultBinding.Apply(stored, [Default]).ToArray();
        Assert.Empty(stored);
        Assert.Equal(0x14, Assert.Single(result).Key);
        Assert.Equal("omni:press", result[0].FeatureCommand);
    }
    [Fact]
    public void MalformedNullEntryRemainsForBindingMapperDiagnostics()
    {
        var result = FeatureDefaultBinding.Apply([null!], [Default]).ToArray();
        Assert.Null(result[0]);
        Assert.Equal("omni.press", result[1].Id);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PersistedBindingWinsEvenWhenDisabled(bool enabled)
    {
        var stored = new PersistedBinding { Id = "OMNI.PRESS", Enabled = enabled, Key = 0x70 };
        Assert.Same(stored, Assert.Single(FeatureDefaultBinding.Apply([stored], [Default])));
    }
}
