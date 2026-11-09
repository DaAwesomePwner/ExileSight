using ExileSight.Core.Overlay;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Window;
using Xunit;

namespace ExileSight.Core.Tests.Overlay;

public sealed class CardPlacementTests
{
    [Theory]
    [InlineData(1920, 1152)]
    [InlineData(2560, 1536)]
    public void DefaultDockFitsAboveCentreAndClearOfMinimap(int width, int expectedX)
    {
        PixelRect client = new(0, 0, width, 1080);
        var settings = new OverlaySettings { PlacementMode = CardPlacementMode.Docked };
        var card = PopupPlacement.PlaceCard(client, client, 0, 0, 300, 400, 1,
            new() { PlacementMode = settings.PlacementMode, DockedX = settings.DockedX, DockedY = settings.DockedY });
        Assert.Equal(new(expectedX, 162, 300, 400), card);
        Assert.True(card.X > width / 2);
        Assert.True(card.X + card.Width <= width * .8);
        Assert.True(card.Y >= 0 && card.Y + card.Height <= client.Height);
    }

    [Theory]
    [InlineData(600, 288)]
    [InlineData(960, 648)] // Exact centre opens left (D-111).
    [InlineData(1100, 1112)]
    public void ChoosesSideAndAlignsTop(int cursorX, int expectedX)
    {
        PixelRect client = new(0, 0, 1920, 1080);
        Assert.Equal(new(expectedX, 200, 300, 400),
            PopupPlacement.PlaceCard(client, client, cursorX, 200, 300, 400, 1, new()));
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1920, 0, 1620, 0)]
    [InlineData(0, 1080, 0, 680)]
    [InlineData(1920, 1080, 1620, 680)]
    [InlineData(500, -100, 188, 0)]
    public void ClampsAllEdges(int x, int y, int expectedX, int expectedY)
    {
        PixelRect client = new(0, 0, 1920, 1080);
        Assert.Equal(new(expectedX, expectedY, 300, 400),
            PopupPlacement.PlaceCard(client, client, x, y, 300, 400, 1, new()));
    }

    [Fact]
    public void ScalesSizeAndHorizontalOffsetOnly()
    {
        PixelRect client = new(0, 0, 3840, 2160);
        Assert.Equal(new(2024, 200, 600, 800),
            PopupPlacement.PlaceCard(client, client, 2000, 200, 300, 400, 2, new()));
    }

    [Theory]
    [InlineData(1920, 1080, 480, 270)]
    [InlineData(2520, 1080, 630, 270)]
    [InlineData(1920, 1200, 480, 300)]
    [InlineData(3840, 2160, 960, 540)]
    public void DockUsesClientFractionsAcrossResolutions(int width, int height, int x, int y)
    {
        PixelRect client = new(0, 0, width, height);
        var settings = new CardPlacementOptions { PlacementMode = CardPlacementMode.Docked, DockedX = .25, DockedY = .25 };
        Assert.Equal(new(x, y, 300, 400),
            PopupPlacement.PlaceCard(client, client, -999, -999, 300, 400, 1, settings));
    }

    [Theory]
    [InlineData(0, 0, -1800, -200)]
    [InlineData(.5, .5, -840, 340)]
    [InlineData(1, 1, -300, 440)]
    public void DockClampsToNegativeMonitorIntersection(double x, double y, int expectedX, int expectedY)
    {
        var settings = new CardPlacementOptions { PlacementMode = CardPlacementMode.Docked, DockedX = x, DockedY = y };
        Assert.Equal(new(expectedX, expectedY, 300, 400),
            PopupPlacement.PlaceCard(new(-1800, -200, 1920, 1080), new(-1920, -240, 1920, 1080),
                0, 0, 300, 400, 1, settings));
    }

    [Theory]
    [InlineData(CardPlacementMode.BesideCursor)]
    [InlineData(CardPlacementMode.Docked)]
    public void OversizedCardFitsAvailableSpace(CardPlacementMode mode)
    {
        Assert.Equal(new(-200, 100, 200, 300),
            PopupPlacement.PlaceCard(new(-200, 100, 600, 500), new(-1920, 0, 1920, 400),
                -100, 200, 3000, 4000, 1, new() { PlacementMode = mode }));
    }

    [Fact]
    public void BesideCursorUsesClientCentreOnOffsetSecondMonitor()
    {
        PixelRect client = new(-1800, -200, 1920, 1080);
        PixelRect work = new(-1920, -240, 1920, 1080);
        Assert.Equal(new(-300, 100, 300, 400),
            PopupPlacement.PlaceCard(client, work, -300, 100, 300, 400, 1, new()));
        Assert.Equal(new(-1152, 100, 300, 400),
            PopupPlacement.PlaceCard(client, work, -840, 100, 300, 400, 1, new()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidDimensionsAndScale(double value)
    {
        PixelRect client = new(0, 0, 1920, 1080);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PopupPlacement.PlaceCard(client, client, 0, 0, value, 400, 1, new()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PopupPlacement.PlaceCard(client, client, 0, 0, 300, value, 1, new()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PopupPlacement.PlaceCard(client, client, 0, 0, 300, 400, value, new()));
    }

    [Fact]
    public void OffscreenClientFallsBackToProvidedWorkArea()
    {
        Assert.Equal(new(0, 0, 300, 400),
            PopupPlacement.PlaceCard(new(-3000, 0, 1920, 1080), new(0, 0, 1920, 1080),
                -2500, 0, 300, 400, 1, new()));
    }
}
