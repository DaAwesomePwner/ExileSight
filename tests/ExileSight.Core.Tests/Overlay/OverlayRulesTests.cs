using ExileSight.Core.Overlay;
using Xunit;

namespace ExileSight.Core.Tests.Overlay;

public sealed class OverlayRulesTests
{
    [Theory]
    [InlineData(96, 1920, 1080)]
    [InlineData(120, 1536, 864)]
    [InlineData(144, 1280, 720)]
    [InlineData(192, 960, 540)]
    public void ConvertsPhysicalBoundsToDips(uint dpi, double width, double height)
    {
        Assert.Equal(new OverlayRect(0, 0, width, height), OverlayRules.ToDips(0, 0, 1920, 1080, dpi));
        Assert.Equal(new OverlayRect(-width, -height, width, height),
            OverlayRules.ToDips(-1920, -1080, 1920, 1080, dpi));
    }

    [Fact]
    public void RejectsUnknownDpi() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => OverlayRules.ToDips(0, 0, 1, 1, 0));

    [Theory]
    [InlineData(1080, 1)]
    [InlineData(1440, 1.3333333333333333)]
    [InlineData(2160, 2)]
    [InlineData(0, 0)]
    public void Uses1080pBaseline(double height, double expected) =>
        Assert.Equal(expected, OverlayRules.Scale(height), 10);

    [Fact]
    public void CentersTwentyOneByNineContentInsideThirtyTwoByNine()
    {
        Assert.Equal(new OverlayRect(880, 0, 3360, 1440), OverlayRules.Content(0, 0, 5120, 1440));
        Assert.Equal(new OverlayRect(-4240, -1440, 3360, 1440),
            OverlayRules.Content(-5120, -1440, 5120, 1440));
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2520, 1080)]
    public void LeavesNarrowerContentUnchanged(double width, double height) =>
        Assert.Equal(new OverlayRect(10, 20, width, height), OverlayRules.Content(10, 20, width, height));

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void RequiresGameForeground(bool found, bool foreground, bool minimized, bool expected) =>
        Assert.Equal(expected, OverlayRules.IsVisible(found, foreground, minimized));

    [Theory]
    [InlineData("POEWindowClass", false, null, true)]
    [InlineData("Other", true, "Path of Exile - GeForce NOW", true)]
    [InlineData(null, true, "path of exile", true)]
    [InlineData("poewindowclass", false, null, false)]
    [InlineData("Other", false, "Path of Exile", false)]
    [InlineData(null, true, "GeForce NOW", false)]
    [InlineData(null, true, null, false)]
    [InlineData(null, true, "", false)]
    [InlineData(null, false, null, false)]
    public void MatchesOnlySupportedClassOrStreamingGameTitle(
        string? windowClass, bool streaming, string? title, bool expected) =>
        Assert.Equal(expected, OverlayRules.Matches(windowClass, streaming, title));

    [Theory]
    [InlineData(0, false)]
    [InlineData(42, true)]
    [InlineData(84, true)]
    [InlineData(21, false)]
    public void MatchesOnlyNonzeroPidsInTheStreamingSnapshot(uint pid, bool expected) =>
        Assert.Equal(expected, OverlayRules.MatchesProcess(pid, new HashSet<uint> { 0, 42, 84 }));

    [Fact]
    public void EmptyStreamingSnapshotDoesNotMatch() =>
        Assert.False(OverlayRules.MatchesProcess(42, new HashSet<uint>()));

    [Theory]
    [InlineData(96, 880)]
    [InlineData(120, 704)]
    [InlineData(144, 586.6666666666666)]
    [InlineData(192, 440)]
    public void ConvertsBlackBarLabelOffsetToDips(uint dpi, double expected)
    {
        var content = OverlayRules.Content(0, 0, 5120, 1440);
        var dips = OverlayRules.ToDips(content.X, content.Y, content.Width, content.Height, dpi);
        Assert.Equal(expected, dips.X, 8);
        Assert.Equal(0, dips.Y);
    }
    [Theory]
    [InlineData("GeForce NOW")]
    [InlineData("")]
    [InlineData(null)]
    public void StreamingMatchIsDroppedWhenTheSameWindowReturnsToLauncher(string? launcherTitle)
    {
        Assert.True(OverlayRules.IsStreamingGameTitle("Path of Exile - GeForce NOW"));
        Assert.False(OverlayRules.IsStreamingGameTitle(launcherTitle));
        Assert.False(OverlayRules.Matches("StreamingWindow", true, launcherTitle));
        Assert.True(OverlayRules.IsStreamingGameTitle("Path of Exile - GeForce NOW"));
    }
}
