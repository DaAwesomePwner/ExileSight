using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.Core.Overlay;

/// <summary>Pure decisions shared by game-move and WPF DPI-change refreshes.</summary>
public readonly record struct OverlayRefresh(bool Reposition, bool Rescale, double ScaleX, double ScaleY)
{
    /// <summary>Compensate WPF's device transform so design units retain the requested physical scale.</summary>
    public static double ContentScale(double physicalScale, double dpiScale)
    {
        if (!double.IsFinite(physicalScale) || physicalScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(physicalScale));
        if (!double.IsFinite(dpiScale) || dpiScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(dpiScale));
        return physicalScale / dpiScale;
    }

    public static OverlayRefresh Decide(PixelRect desired, PixelRect actual, double physicalScale,
        double dpiX, double dpiY, double currentScaleX, double currentScaleY, bool justShown)
    {
        var x = ContentScale(physicalScale, dpiX);
        var y = ContentScale(physicalScale, dpiY);
        return new(justShown || desired != actual, x != currentScaleX || y != currentScaleY, x, y);
    }
}
