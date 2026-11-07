using ExileSight.Core.Overlay;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.Platform.Windows.Window;

/// <summary>UI-thread detection with a bounded, invalidatable GFN process snapshot.</summary>
internal sealed class GameWindowDetection(IWindowSource source, TimeProvider clock)
{
    private IReadOnlySet<uint>? streamingPids;
    private long snapshotTime;
    private bool streaming;

    internal void InvalidateSnapshot() => streamingPids = null;

    private IReadOnlySet<uint> Snapshot()
    {
        if (streamingPids is null || clock.GetElapsedTime(snapshotTime) >= TimeSpan.FromMilliseconds(1500))
        {
            streamingPids = source.StreamingPids();
            snapshotTime = clock.GetTimestamp();
        }
        return streamingPids;
    }

    internal bool IsStreamingGame(WindowCandidate candidate) =>
        OverlayRules.MatchesStreaming(candidate.Pid, Snapshot(), candidate.Title);

    internal GameWindowState Refresh(GameWindowState previous, out uint pid)
    {
        if (!previous.Found) InvalidateSnapshot();
        var candidate = source.Read(previous.Handle);
        if (candidate is null || (streaming && !IsStreamingGame(candidate)))
        {
            candidate = null;
            var snapshot = Snapshot();
            foreach (var window in source.Enumerate())
            {
                if (!OverlayRules.Matches(window.Class,
                    OverlayRules.MatchesProcess(window.Pid, snapshot), window.Title)) continue;
                candidate = window;
                streaming = !OverlayRules.Matches(window.Class, false, null);
                break;
            }
        }
        var state = candidate is null ? GameWindowState.Missing : source.State(candidate.Handle);
        pid = state.Found ? candidate!.Pid : 0;
        return state with { LastEventTime = clock.GetUtcNow() };
    }
}
