namespace ExileSight.Platform.Abstractions.Window;

/// <summary>Operations reject windows not owned by this process.</summary>
public interface IOverlayWindowStyler
{
    void Apply(nint handle);
    void SetClickThrough(nint handle, bool enabled);
    PixelRect GetBounds(nint handle);
    /// <summary>Physical work area of the monitor nearest the reference rectangle.</summary>
    PixelRect GetWorkArea(PixelRect reference);
    void Position(nint handle, PixelRect rect);
}
