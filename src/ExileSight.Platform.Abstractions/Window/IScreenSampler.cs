namespace ExileSight.Platform.Abstractions.Window;

public interface IScreenSampler
{
    /// <summary>
    /// region is relative to the game client's top-left, in physical pixels.
    /// Returns null if the game is not found, not foreground, minimized, the region is empty,
    /// not fully inside the current client rect, larger than MaxPixels, or capture fails.
    /// </summary>
    ScreenSample? TrySample(PixelRect region);
    const int MaxPixels = 1_048_576;
}
