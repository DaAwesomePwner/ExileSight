using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.Core.Overlay;

/// <summary>Physical placement is independent of the overlay monitor's current DPI.</summary>
public sealed class OverlayPlacement
{
    public PixelRect Calculate(PixelRect client, OverlayRect design, double? scaleOverride)
    {
        var rect = OverlayRules.Place(new(client.X, client.Y, client.Width, client.Height), design, scaleOverride);
        return new((int)Math.Round(rect.X), (int)Math.Round(rect.Y),
            Math.Max(1, (int)Math.Ceiling(rect.Width)), Math.Max(1, (int)Math.Ceiling(rect.Height)));
    }

    // WPF can change the native rect after WM_DPICHANGED; previous requested bounds are not evidence
    // of the current geometry. Showing again also requires reasserting topmost, even at equal bounds.
    public bool NeedsPosition(PixelRect computed, PixelRect actual, bool justShown) =>
        justShown || computed != actual;
}
