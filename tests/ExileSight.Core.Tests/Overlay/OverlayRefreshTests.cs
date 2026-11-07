using ExileSight.Core.Overlay;
using ExileSight.Platform.Abstractions.Window;
using Xunit;

namespace ExileSight.Core.Tests.Overlay;

public sealed class OverlayRefreshTests
{
    [Theory]
    [InlineData(1920, 0)]
    [InlineData(-1920, -1080)]
    public void MoveTo150PercentMonitorAndBackRepositionsAndRescales(int x, int y)
    {
        var placement = new OverlayPlacement();
        var design = new OverlayRect(20, 80, 400, 530);
        var primary = placement.Calculate(new(0, 0, 1920, 1080), design, 1.33);
        var secondary = placement.Calculate(new(x, y, 1920, 1080), design, 1.33);
        var move = OverlayRefresh.Decide(secondary, primary, 1.33, 1.5, 1.5, 1.33, 1.33, false);
        Assert.True(move.Reposition);
        Assert.True(move.Rescale);
        Assert.Equal(1.33 / 1.5, move.ScaleX, 10);
        Assert.Equal(532, 400 * move.ScaleX * 1.5, 8);
        var settled = OverlayRefresh.Decide(secondary, secondary, 1.33, 1.5, 1.5, move.ScaleX, move.ScaleY, false);
        Assert.False(settled.Reposition);
        Assert.False(settled.Rescale);
        var back = OverlayRefresh.Decide(primary, secondary, 1.33, 1, 1, move.ScaleX, move.ScaleY, false);
        Assert.True(back.Reposition);
        Assert.True(back.Rescale);
        Assert.Equal(1.33, back.ScaleX);
        Assert.Equal(1.33, back.ScaleY);
    }

    [Fact]
    public void DpiChangeCorrectsSuggestedBoundsAndRescalesWithoutGameMovement()
    {
        var desired = new PixelRect(20, 80, 400, 530);
        var refresh = OverlayRefresh.Decide(desired, new(20, 80, 600, 795), 1, 1.5, 1.5, 1, 1, false);
        Assert.True(refresh.Reposition);
        Assert.True(refresh.Rescale);
        Assert.Equal(400, 400 * refresh.ScaleX * 1.5, 8);
        Assert.True(OverlayRefresh.Decide(desired, desired, 1, 1, 1, 1, 1, true).Reposition);
        var dpiOnly = OverlayRefresh.Decide(desired, desired, 1, 1.5, 1.5, 1, 1, false);
        Assert.False(dpiOnly.Reposition);
        Assert.True(dpiOnly.Rescale);
    }

    [Theory]
    [InlineData(500, 400, 0, 0, 1920, 1080, 1, 300, 250, 400, 300)]
    [InlineData(-2000, -100, -1920, 0, 1920, 1080, 2, -1920, 0, 800, 600)]
    [InlineData(999, 999, 10, 20, 100, 100, 1, 10, 20, 100, 100)]
    public void LiveWheelPlacementPreservesCenterOrClamps(int cx, int cy, int x, int y, int w, int h,
        double scale, int expectedX, int expectedY, int expectedW, int expectedH)
    {
        var area = new PixelRect(x, y, w, h);
        Assert.Equal(new PixelRect(expectedX, expectedY, expectedW, expectedH),
            PopupPlacement.Place(area, area, cx, cy, 400, 300, scale, centered: true));
    }

    [Fact]
    public void PopupMovesBeforeShrinkingAndShrinksOnlyWhenTooLarge()
    {
        var area = new PixelRect(0, 0, 800, 600);
        Assert.Equal(new PixelRect(370, 400, 430, 200),
            PopupPlacement.Place(area, area, 790, 590, 430, 200, 1, 12));
        Assert.Equal(new PixelRect(0, 0, 800, 600),
            PopupPlacement.Place(area, area, 790, 590, 900, 700, 1, 12));
    }
}
