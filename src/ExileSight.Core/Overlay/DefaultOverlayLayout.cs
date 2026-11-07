namespace ExileSight.Core.Overlay;

/// <summary>Non-overlapping 1080p regions, reserving the upper left for generic panels and toasts.</summary>
public static class DefaultOverlayLayout
{
    public static OverlayRect Guide => new(440, 470, 460, 440);
    public static OverlayRect Timers => new(20, 340, 400, 530);
}
