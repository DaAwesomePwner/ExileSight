namespace ExileSight.Platform.Abstractions.Window;

public interface IScreenSampler
{
    /// <summary>
    /// region is relative to the game client's top-left, in physical pixels.
    /// Returns null if the game is not found, not foreground, minimized, the region is empty,
    /// not fully inside the current client rect, larger than MaxPixels, or capture fails.
    /// </summary>
    ScreenSample? TrySample(PixelRect region);
    /// <summary>Wait once after hiding overlays and submitting UI render work, before a capture batch.</summary>
    void WaitForComposition();
    const int MaxPixels = 1_048_576;
}

/// <summary>Bounds the caller's wait and retains at most one blocked composition worker.</summary>
public sealed class CompositionWait(IScreenSampler sampler)
{
    public const int TimeoutMilliseconds = 100;
    public const int FallbackMilliseconds = 32;
    private readonly object sync = new();
    private Task? pending;

    public async Task WaitAsync()
    {
        Task current;
        bool reused;
        lock (sync)
        {
            // A timed-out native call cannot be cancelled. Reuse it until it finishes,
            // rather than accumulating blocked threads on subsequent captures.
            reused = pending is { IsCompleted: false };
            if (!reused)
                pending = Task.Factory.StartNew(sampler.WaitForComposition, CancellationToken.None,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
            current = pending!;
        }
        try
        {
            await current.WaitAsync(TimeSpan.FromMilliseconds(TimeoutMilliseconds)).ConfigureAwait(false);
            // An older flush may predate this batch's hide, so it still needs the fallback.
            if (reused) await Task.Delay(FallbackMilliseconds).ConfigureAwait(false);
        }
        catch (TimeoutException)
        { await Task.Delay(FallbackMilliseconds).ConfigureAwait(false); }
    }
}
