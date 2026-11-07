using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.Core.Overlay;

/// <summary>A failed detection or hook installation hides overlays and keeps detection alive.</summary>
public sealed record TrackingRefresh(GameWindowState State, string? Error)
{
    public int RetryMilliseconds => State.Found ? Timeout.Infinite : 1500;

    public static TrackingRefresh Run(Func<GameWindowState> detectAndHook)
    {
        try { return new(detectAndHook(), null); }
        catch (Exception error)
        {
            return new(GameWindowState.Missing with { LastEventTime = DateTimeOffset.UtcNow }, error.Message);
        }
    }
}
