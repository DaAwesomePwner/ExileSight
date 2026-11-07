using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Window;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Window;

public sealed class OverlayPlatformTests
{
    [WindowsOnlyFact]
    public void NativeStylerRejectsUnownedHandlesBeforeAnyMutation()
    {
        var styler = new OverlayWindowStyler();
        Assert.Throws<ArgumentException>(() => styler.Apply(0));
        Assert.Throws<ArgumentException>(() => styler.SetClickThrough(0, false));
        Assert.Throws<ArgumentException>(() => styler.Position(0, new PixelRect(0, 0, 100, 100)));
        Assert.Throws<ArgumentException>(() => styler.Apply(-123));
    }

    [WindowsOnlyFact]
    public void RegistersNativeWinEventsAndDisposesOnTheOwningThread()
    {
        // Startup/disposal needs no generated events: exercise real hook installation,
        // initial native enumeration and idempotent unhooking on the same thread.
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new HoldingContext());
        try
        {
            using var tracker = new GameWindowTracker();
            int changes = 0;
            EventHandler failingHandler = (_, _) => throw new InvalidOperationException("subscriber failed");
            tracker.Changed += failingHandler;
            tracker.Changed += (_, _) => changes++;
            tracker.Start();
            Assert.Equal(1, changes);
            Assert.Equal("subscriber failed", tracker.LastError);
            Assert.NotEqual(default, tracker.Current.LastEventTime);
            tracker.Changed -= failingHandler;
            // Drive the same refresh used by native events without requiring a live game.
            typeof(GameWindowTracker).GetMethod("Refresh",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(tracker, null);
            Assert.Equal(2, changes);
            Assert.Null(tracker.LastError);
            tracker.Dispose();
            tracker.Dispose();
            Assert.Throws<ObjectDisposedException>(() => tracker.Start());
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private sealed class HoldingContext : SynchronizationContext
    {
        // Deliberately hold callbacks until teardown; no work can escape the test.
        public override void Post(SendOrPostCallback callback, object? state) { }
    }

    [WindowsOnlyFact]
    public void SamplesOnlyOwnProcessWithBoundedCpu()
    {
        using var stats = new OwnProcessStats();
        var first = stats.Sample();
        var second = stats.Sample();
        Assert.True(first.WorkingSetBytes > 0);
        Assert.True(first.PrivateBytes > 0);
        Assert.Equal(0, first.CpuPercent);
        Assert.InRange(second.CpuPercent, 0, 100);
    }
}
