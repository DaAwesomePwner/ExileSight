using ExileSight.Core.Overlay;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.Platform.Windows.Window;

/// <summary>UI-thread detection with a bounded, invalidatable GFN process snapshot.</summary>
internal sealed class GameWindowDetection(IWindowSource source, TimeProvider clock)
{
    internal static GameWindowDetection Shared { get; } = new(new WindowSource(), TimeProvider.System);
    private readonly object sync = new();
    private IReadOnlySet<uint>? streamingPids;
    private long snapshotTime;
    private bool streaming;

    internal void InvalidateSnapshot() { lock (sync) streamingPids = null; }

    internal bool Matches(nint handle)
    {
        lock (sync)
        {
            var native = source.Read(handle, new HashSet<uint>());
            if (native is null) return false;
            if (OverlayRules.Matches(native.Class, false, null)) return true;
            var snapshot = Snapshot();
            var candidate = source.Read(handle, snapshot);
            return candidate is not null && OverlayRules.Matches(candidate.Class,
                snapshot.Contains(candidate.Pid), candidate.Title);
        }
    }

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
        lock (sync) return RefreshLocked(previous, out pid);
    }

    private GameWindowState RefreshLocked(GameWindowState previous, out uint pid)
    {
        if (!previous.Found) InvalidateSnapshot();
        var snapshot = Snapshot();
        var candidate = source.Read(previous.Handle, snapshot);
        if (candidate is null || (streaming && !IsStreamingGame(candidate)))
        {
            candidate = null;
            foreach (var window in source.Enumerate(snapshot))
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
