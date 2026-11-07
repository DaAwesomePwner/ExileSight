using Xunit;
using ExileSight.Core.Overlay;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.Core.Tests.Overlay;

public sealed class DpiMatrixTests
{
    public static TheoryData<double, uint> Scales => new()
    { { 1, 96 }, { 1.25, 120 }, { 1.33, 128 }, { 1.5, 144 }, { 1.75, 168 }, { 2, 192 } };

    [Theory]
    [MemberData(nameof(Scales))]
    public void PhysicalSizeSurvivesDestinationDpiConversion(double scale, uint dpi)
    {
        foreach (var client in new[] { new PixelRect(1920, 40, 1920, 1080), new PixelRect(-2560, -400, 2560, 1440) })
        {
            var physical = new OverlayPlacement().Calculate(client, new(20, 80, 400, 530), scale);
            Assert.Equal(client.X + (int)Math.Round(20 * scale), physical.X);
            Assert.Equal(client.Y + (int)Math.Round(80 * scale), physical.Y);
            Assert.Equal((int)Math.Ceiling(400 * scale), physical.Width);
            // This is the transform consumed by WPF, followed by WPF's device transform.
            var transform = OverlayRefresh.ContentScale(scale, dpi / 96d);
            Assert.Equal(400 * scale, 400 * transform * (dpi / 96d), 8);
            Assert.Equal(530 * scale, 530 * transform * (dpi / 96d), 8);
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void PopupFitsWorkAreaAtEveryCorner(double scale, uint dpi)
    {
        var client = new PixelRect(-1920, -200, 1920, 1080);
        var work = new PixelRect(-1880, -200, 1880, 1040);
        foreach (int x in new[] { -1920, -1 })
        foreach (int y in new[] { -200, 879 })
        {
            var rect = PopupPlacement.Place(client, work, x, y, 430, 600, scale, 12);
            Assert.InRange(rect.X, work.X, work.X + work.Width - rect.Width);
            Assert.InRange(rect.Y, work.Y, work.Y + work.Height - rect.Height);
            Assert.Equal(Math.Min(1040, (int)Math.Ceiling(600 * scale)), rect.Height);
            Assert.Equal(430 * scale,
                430 * OverlayRefresh.ContentScale(scale, dpi / 96d) * (dpi / 96d), 8);
        }
    }

    [Fact]
    public void CenteredWheelClampsAndAutomaticScaleFollowsClientHeight()
    {
        Assert.Equal(new PixelRect(-300, 740, 300, 300),
            PopupPlacement.Place(new(-300, 0, 300, 1080), new(-300, 0, 300, 1040),
                -1, 1079, 400, 300, 1, centered: true));
        var placement = new OverlayPlacement();
        Assert.Equal(new PixelRect(1940, 80, 400, 530),
            placement.Calculate(new(1920, 0, 1920, 1080), new(20, 80, 400, 530), null));
        Assert.Equal(new PixelRect(-2533, -293, 534, 707),
            placement.Calculate(new(-2560, -400, 2560, 1440), new(20, 80, 400, 530), null));
    }

    [Fact]
    public void PopupPreservesCursorOffsetAndHandlesOffscreenClient()
    {
        Assert.Equal(new PixelRect(116, 216, 572, 266),
            PopupPlacement.Place(new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 100, 200, 430, 200, 1.33, 12));
        Assert.Equal(new PixelRect(0, 0, 100, 100),
            PopupPlacement.Place(new(-2000, 0, 100, 100), new(0, 0, 100, 100), -2000, 0, 400, 400, 1, 12));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void DefaultPanelsNeverOverlap(double scale, uint dpi)
    {
        foreach (bool compactTimers in new[] { false, true })
        {
            var designs = DefaultOverlayLayout.FixedRegions(compactTimers);
            var placement = new OverlayPlacement();
            var rects = designs.Select(d => placement.Calculate(new(-1920, 0, 1920, 1080), d, scale)).ToArray();
            for (int i = 0; i < rects.Length; i++)
            for (int j = i + 1; j < rects.Length; j++)
            {
                var a = rects[i]; var b = rects[j];
                Assert.True(a.X + a.Width <= b.X || b.X + b.Width <= a.X || a.Y + a.Height <= b.Y || b.Y + b.Height <= a.Y,
                    $"Panels {i}/{j} overlap at {scale}/{dpi}");
            }
        }
    }
}
