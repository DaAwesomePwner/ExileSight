using System.ComponentModel;
using ExileSight.Core.Overlay;
using ExileSight.Platform.Abstractions.Window;
using Xunit;

namespace ExileSight.Core.Tests.Overlay;

public sealed class ProductionOverlayTests
{
    [Theory]
    [InlineData(1080, null, 8, 160)]
    [InlineData(1440, null, 10.666666666666666, 213.33333333333333)]
    [InlineData(1440, 2d, 16, 320)]
    public void ScalesBothOffsetsAndWindowSize(double height, double? scale, double x, double width)
    {
        var placed = OverlayRules.Place(new(-1920, -100, 1920, height), new(8, 8, 160, 32), scale);
        Assert.Equal(-1920 + x, placed.X, 8);
        Assert.Equal(-100 + x, placed.Y, 8);
        Assert.Equal(width, placed.Width, 8);
    }

    [Fact]
    public void AnchorsToCompensatedContentBeforeApplyingOverride()
    {
        Assert.Equal(new OverlayRect(-4224, -1424, 320, 64),
            OverlayRules.Place(new(-5120, -1440, 5120, 1440), new(8, 8, 160, 32), 2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidOverridesFallBackToClientHeight(double value) =>
        Assert.Equal(2, OverlayRules.Scale(2160, value));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public void SettingsAndPerWindowToggleBothControlVisibility(bool enabled, bool requested, bool expected) =>
        Assert.Equal(expected, OverlayRules.IsVisible(true, true, false, enabled, requested));

    [Fact]
    public void HookFailureSurfacesErrorAndContinuesDetectionUntilRecovery()
    {
        int attempts = 0;
        GameWindowState DetectAndHook()
        {
            attempts++;
            if (attempts < 3) throw new Win32Exception(5, "SetWinEventHook failed");
            return new(true, 42, new(0, 0, 1920, 1080), 96, true, false, DateTimeOffset.UtcNow);
        }
        for (int retry = 0; retry < 2; retry++)
        {
            var failed = TrackingRefresh.Run(DetectAndHook);
            Assert.False(failed.State.Found);
            Assert.Equal("SetWinEventHook failed", failed.Error);
            Assert.Equal(1500, failed.RetryMilliseconds);
        }
        var recovered = TrackingRefresh.Run(DetectAndHook);
        Assert.True(recovered.State.Found);
        Assert.Null(recovered.Error);
        Assert.Equal(Timeout.Infinite, recovered.RetryMilliseconds);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public void DisappearingStreamingProcessDropsFoundStateEvenWithStaleTitle()
    {
        var pids = new HashSet<uint> { 42 };
        GameWindowState Detect() => OverlayRules.MatchesStreaming(42, pids, "Path of Exile")
            ? new(true, 123, default, 96, true, false, default) : GameWindowState.Missing;
        Assert.True(TrackingRefresh.Run(Detect).State.Found);
        pids.Clear();
        var dropped = TrackingRefresh.Run(Detect);
        Assert.False(dropped.State.Found);
        Assert.Equal(1500, dropped.RetryMilliseconds);
    }
}
