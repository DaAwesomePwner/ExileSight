using System.Collections.Concurrent;
using System.ComponentModel;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Window;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Window;

public sealed class TrackerRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HookFailurePublishesLastErrorAndTimerRetriesUntilRecovery(bool scopedFailure)
    {
        var previous = SynchronizationContext.Current;
        using var context = new PumpContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var source = new Source();
            var hooks = new Hooks { Failing = true, ScopedFailure = scopedFailure };
            using var tracker = new GameWindowTracker(hooks, new(source, new Clock()));
            tracker.Start();
            Assert.False(tracker.Current.Found);
            Assert.Equal("SetWinEventHook failed", tracker.LastError);
            context.PumpTimer();
            Assert.False(tracker.Current.Found);
            Assert.Equal("SetWinEventHook failed", tracker.LastError);
            Assert.Equal(2, hooks.Attempts);
            hooks.Failing = false;
            context.PumpTimer();
            Assert.True(tracker.Current.Found);
            Assert.Null(tracker.LastError);
            Assert.Equal(3, hooks.Attempts);
            Assert.True(source.Snapshots >= 1);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    [Fact]
    public void StreamingDropAndSnapshotCacheUseRealDetectionAndTrackerEvents()
    {
        var previous = SynchronizationContext.Current;
        using var context = new PumpContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var source = new Source();
            var clock = new Clock();
            using var tracker = new GameWindowTracker(new Hooks(), new(source, clock));
            tracker.Start();
            Assert.True(tracker.Current.Found);
            Assert.Equal(1, source.Snapshots);
            for (int i = 0; i < 100; i++) tracker.HandleEvent(WinEvents.LocationChange, 123);
            context.PumpTimer(); // Coalesced native event callback, not a timer tick.
            Assert.Equal(1, source.Snapshots);
            clock.Advance();
            tracker.HandleEvent(WinEvents.LocationChange, 123);
            context.PumpTimer();
            Assert.Equal(2, source.Snapshots);
            Assert.True(tracker.Current.Found);
            source.Pids.Clear(); // HWND/title stay stale after the streaming process disappears.
            tracker.HandleEvent(WinEvents.NameChange, 123);
            context.PumpTimer();
            Assert.False(tracker.Current.Found);
            Assert.Null(tracker.LastError);
            Assert.Equal(3, source.Snapshots);
            context.PumpTimer(); // Missing-state redetection must refresh the process snapshot.
            Assert.Equal(4, source.Snapshots);
            source.Pids.Add(42);
            context.PumpTimer();
            Assert.True(tracker.Current.Found);
            source.Pids.Clear();
            tracker.HandleEvent(WinEvents.Destroy, 123);
            context.PumpTimer();
            Assert.False(tracker.Current.Found);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private sealed class Clock : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public void Advance() => ticks += TimeSpan.FromMilliseconds(1500).Ticks;
    }

    private sealed class Source : IWindowSource
    {
        internal HashSet<uint> Pids { get; } = [42];
        internal int Snapshots { get; private set; }
        private readonly WindowCandidate window = new(123, 42, "StreamingWindow", "Path of Exile");
        public IReadOnlySet<uint> StreamingPids() { Snapshots++; return Pids.ToHashSet(); }
        public IReadOnlyList<WindowCandidate> Enumerate(IReadOnlySet<uint> streamingPids) => [window];
        public WindowCandidate? Read(nint handle, IReadOnlySet<uint> streamingPids) => handle == window.Handle ? window : null;
        public GameWindowState State(nint handle) => new(true, handle, new(0, 0, 1920, 1080), 96, true, false, default);
    }

    private sealed class Hooks : IGameWindowHooks
    {
        internal bool Failing { get; set; }
        internal bool ScopedFailure { get; init; }
        internal int Attempts { get; private set; }
        public void Start()
        {
            Attempts++;
            if (Failing && !ScopedFailure) throw new Win32Exception(5, "SetWinEventHook failed");
        }
        public void ScopeTo(uint pid)
        {
            if (Failing && ScopedFailure) throw new Win32Exception(5, "SetWinEventHook failed");
        }
        public void Dispose() { }
    }

    private sealed class PumpContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = new();
        public override void Post(SendOrPostCallback callback, object? state) => queue.Add((callback, state));
        internal void PumpTimer()
        {
            Assert.True(queue.TryTake(out var work, TimeSpan.FromSeconds(10)), "Tracker did not enqueue a refresh.");
            work.Callback(work.State);
        }
        public void Dispose() => queue.Dispose();
    }
}
