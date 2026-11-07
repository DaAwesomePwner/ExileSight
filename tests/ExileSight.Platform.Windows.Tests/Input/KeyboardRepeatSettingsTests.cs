using ExileSight.Platform.Windows.Input;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Input;

public sealed class KeyboardRepeatSettingsTests
{
    [Theory]
    [InlineData(false, 2000u, 2000u, 2000u, 1200L)]
    [InlineData(true, 0u, 1000u, 30u, 1200L)]
    [InlineData(true, 0u, 2000u, 30u, 2200L)]
    [InlineData(true, 0u, 1000u, 2000u, 2200L)]
    [InlineData(true, 2000u, 2000u, 2000u, 4200L)]
    [InlineData(true, 20000u, 20000u, 20000u, 40200L)]
    [InlineData(true, uint.MaxValue, uint.MaxValue, uint.MaxValue, 8589934790L)]
    public void FilterKeysTimingIncludesAcceptanceDelayWithoutOverflow(
        bool enabled, uint wait, uint delay, uint repeat, long expected) =>
        Assert.Equal(expected, KeyboardRepeatSettings.Calculate(enabled, wait, delay, repeat));

    [WindowsOnlyFact]
    public void NativeRefreshPublishesAValidSnapshotWithoutChangingSystemSettings()
    {
        var settings = new KeyboardRepeatSettings();
        Assert.Equal(long.MaxValue, settings.SilenceMilliseconds);
        settings.Refresh();
        Assert.InRange(settings.SilenceMilliseconds, 1200, long.MaxValue - 1);
    }
}
