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
    [InlineData("POEWindowClass", null, true)]
    [InlineData("Other", "GeForceNOW", true)]
    [InlineData(null, "geforcenow", true)]
    [InlineData("poewindowclass", null, false)]
    [InlineData("Other", "PathOfExile", false)]
    [InlineData(null, "GeForceNOW.exe", false)]
    [InlineData(null, null, false)]
    public void MatchesOnlySupportedClassOrProcess(string? windowClass, string? process, bool expected) =>
        Assert.Equal(expected, OverlayRules.Matches(windowClass, process));
}
