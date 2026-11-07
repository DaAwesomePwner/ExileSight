using ExileSight.Core.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class PassiveInputTests
{
    [Fact]
    public void HiddenMenuHasNoPendingEventsEvenAfterEightThousandMoves()
    {
        var channel = new PassiveInputChannel();
        var events = new List<PassiveInput>();
        channel.Observed += events.Add;
        for (var i = 0; i < 8000; i++) channel.Observe(0x200, i, i, i);
        channel.Observe(0x100, 0, 0, 9000);
        channel.Drain();
        channel.Open(9000);
        channel.Drain();
        Assert.Empty(events);
    }
    [Fact]
    public void FloodCoalescesMovesButPreservesEveryClickAndEscape()
    {
        var channel = new PassiveInputChannel();
        channel.Open(1);
        var events = new List<PassiveInput>();
        channel.Observed += events.Add;
        for (var i = 1; i <= 8000; i++)
        {
            channel.Observe(0x200, i, i, i);
            if (i % 100 == 0) channel.Observe(0x201, i, i, i);
        }
        channel.Observe(0x100, 0, 0, 8001);
        channel.Drain();
        Assert.Equal(80, events.Count(e => e.Message == 0x201));
        Assert.Single(events, e => e.Message == 0x100);
        Assert.Equal(8000, Assert.Single(events, e => e.Message == 0x200).X);
        channel.Drain();
        Assert.Equal(82, events.Count);
    }
    [Fact]
    public void ReopenedMenuRejectsPreviousEpochAndOldHookTimestamps()
    {
        var channel = new PassiveInputChannel();
        var events = new List<PassiveInput>();
        channel.Observed += events.Add;
        channel.Open(100);
        channel.Observe(0x100, 0, 0, 110);
        channel.Observe(0x200, 10, 10, 120);
        channel.Close();
        channel.Open(200);
        channel.Observe(0x100, 0, 0, 199);
        channel.Observe(0x201, 0, 0, 201);
        channel.Drain();
        Assert.Equal(201, Assert.Single(events).Timestamp);
    }
    [Fact]
    public void ObserverExceptionsDoNotStopOtherSubscribersOrLaterEvents()
    {
        var channel = new PassiveInputChannel();
        channel.Open(0);
        channel.Observed += _ => throw new InvalidOperationException();
        var calls = 0;
        channel.Observed += _ => calls++;
        channel.Observe(0x100, 0, 0, 1);
        channel.Drain();
        channel.Observe(0x201, 0, 0, 2);
        channel.Drain();
        Assert.Equal(2, calls);
    }
    [Fact]
    public void OverflowRequestsDismissalOnDrainAndDoesNotLeakIntoNewEpoch()
    {
        var channel = new PassiveInputChannel();
        var events = new List<PassiveInput>();
        channel.Observed += events.Add;
        channel.Open(0);
        for (var i = 0; i < 10000; i++) channel.Observe(0x201, 1, 1, i);
        Assert.Empty(events); // Hook callback only enqueues.
        channel.Drain();
        Assert.Equal(0x100u, Assert.Single(events).Message);
        Assert.True(channel.IsInterested);
        channel.Open(10000);
        channel.Drain();
        Assert.Single(events);
        channel.Observe(0x100, 0, 0, 10001);
        channel.Drain();
        Assert.Equal(10001, events.Last().Timestamp);
        Assert.Equal(2, events.Count);
    }
    [Fact]
    public void OpenDuringUiStallIgnoresOldOverflowButReceivesLaterEscape()
    {
        var channel = new PassiveInputChannel();
        channel.Open(0);
        long openedAt = 0;
        var visible = true;
        channel.Observed += input =>
        {
            if (input.Timestamp >= openedAt && input.Message == 0x100) visible = false;
        };
        for (var i = 0; i <= PassiveInputChannel.Capacity; i++) channel.Observe(0x201, 1, 1, 3000);
        // Normal-priority OpenCore replaces the observer before the Background drain.
        // The shared lease is still open, so PassiveOverlayInput does not call Open again.
        openedAt = 5001;
        channel.Drain();
        Assert.True(visible);
        Assert.True(channel.IsInterested);
        channel.Observe(0x100, 0, 0, 5002);
        channel.Drain();
        Assert.False(visible);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverflowResetDoesNotOverwriteObserverCloseOrReopen(bool reopen)
    {
        var channel = new PassiveInputChannel();
        channel.Open(0);
        channel.Observed += _ => { if (reopen) channel.Open(5000); else channel.Close(); };
        for (var i = 0; i <= PassiveInputChannel.Capacity; i++) channel.Observe(0x201, 1, 1, 3000);
        channel.Drain();
        Assert.Equal(reopen, channel.IsInterested);
        var count = 0;
        channel.Observed += _ => count++;
        channel.Observe(0x100, 0, 0, 4000);
        channel.Drain();
        Assert.Equal(0, count);
    }
    [Fact]
    public void ReportsOnlyObserverExceptionTypeEvenWhenReporterFails()
    {
        var channel = new PassiveInputChannel();
        var errors = new List<string>();
        channel.ReportFailure = type => { errors.Add(type); throw new IOException(); };
        channel.Observed += _ => throw new InvalidOperationException("private item");
        var calls = 0;
        channel.Observed += _ => calls++;
        channel.Open(0);
        channel.Observe(0x100, 0, 0, 1);
        channel.Drain();
        Assert.Equal(["InvalidOperationException"], errors);
        Assert.Equal(1, calls);
    }
    [Fact]
    public void ObserverReopeningMenuCannotDeliverOldEventToNextObserver()
    {
        var channel = new PassiveInputChannel();
        channel.Open(0);
        channel.Observed += _ => channel.Open(100);
        channel.Observed += _ => Assert.Fail("Old event reached new visibility epoch");
        channel.Observe(0x100, 0, 0, 1);
        channel.Drain();
    }
}
