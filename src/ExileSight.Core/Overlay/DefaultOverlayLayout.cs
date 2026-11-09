namespace ExileSight.Core.Overlay;

/// <summary>1080p layout inventory. Compact and expanded timers occupy the same reserved slot.</summary>
public static class DefaultOverlayLayout
{
    public static OverlayRect Status => new(440, 20, 160, 32);
    public static OverlayRect Lab => new(920, 470, 460, 440);
    public static OverlayRect Guide => new(440, 470, 460, 440);
    public static OverlayRect Timers => new(20, 340, 400, 530);
    public static OverlayRect CompactTimers => Timers with { Height = 220 };
    public static OverlayRect TimersToast => new(440, 80, 300, 100);

    public static OverlayRect GenericPanel => new(20, 20, 400, 300);
    public static OverlayRect EventsToast => new(500, 340, 340, 110);
    public static OverlayRect StashRegistration => new(0, 0, 1, 1);

    public static IReadOnlyList<OverlayRect> FixedRegions(bool compactTimers) =>
        [Status, Guide, Lab, compactTimers ? CompactTimers : Timers, TimersToast,
            GenericPanel, EventsToast, StashRegistration];
}
