using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class FinalReviewRegressionTests
{
    [Fact]
    public void LateReaderHoldingClipboardIsRenderedDuringRestoreRetries()
    {
        var h = new ActionHarness();
        h.Clipboard.ConsumeAfterPumps = int.MaxValue;
        var readServed = false;
        h.Clipboard.OnPump = () =>
        {
            if (h.Clock.Milliseconds >= 10800)
                readServed = h.Clipboard.PasteConsumed = true;
        };
        h.Clipboard.BeforeRestore = () =>
        {
            if (!readServed) throw new IOException("Reader holds clipboard while awaiting WM_RENDERFORMAT");
        };
        var result = h.Run(new GameAction.SendChatCommand("/hideout"));
        Assert.True(readServed);
        Assert.Equal(10800, h.Clock.Milliseconds);
        Assert.Equal(3, h.Clipboard.RestoreCalls);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
        Assert.Equal(ActionOutcome.Success, result.Outcome);
        Assert.Equal("Sent (paste confirmed)", result.PasteStatus);
        Assert.Contains("Sent (paste confirmed)", Assert.Single(h.Audit.Lines));
    }
    [Theory]
    [InlineData(false, 750)]
    [InlineData(true, 1500)]
    public void SendExceptionWithUnknownInsertionCountStillHoldsAndRestores(bool streaming, int hold)
    {
        var h = new ActionHarness();
        h.Sender.IsStreaming = streaming;
        h.Sender.OnSend = () => throw new IOException("failure after insertion");
        h.Clipboard.BeforeRestore = () => Assert.True(h.Clock.Milliseconds >= 10000 + hold);
        var action = new GameAction.SendChatCommand("/hideout");
        var token = h.Token(action);
        var result = h.Gate.TryExecute(token, action);
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Contains("insertion count unknown", result.Reason);
        Assert.Equal(10000 + hold, h.Clock.Milliseconds);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, h.Gate.TryExecute(token, action).Outcome);
        Assert.Equal(1, h.Sender.Calls);
    }
    [Fact]
    public void CopySendExceptionAlsoWaitsBeforeRestoring()
    {
        var h = new ActionHarness();
        h.Sender.OnSend = () => throw new IOException("partial copy");
        var result = h.Run(new GameAction.CopyItem());
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Contains("insertion count unknown", result.Reason);
        Assert.Equal(10750, h.Clock.Milliseconds);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
    }
    [Fact]
    public void SkippedFormatsAreIdentifiedInOutcomeAndAudit()
    {
        var h = new ActionHarness();
        h.Clipboard.SkippedFormats = [2, 49152];
        var result = h.Run(new GameAction.SendChatCommand("/hideout"));
        Assert.Contains("0x0002", result.Reason);
        Assert.Contains("0xC000", result.Reason);
        Assert.Contains("0xC000", Assert.Single(h.Audit.Lines));
    }
    [Fact]
    public void StreamingCopyAcceptsLateCloudTextEvenIfForegroundChangesAfterSending()
    {
        var h = new ActionHarness();
        h.Sender.IsStreaming = true;
        h.Sender.OnSend = () => h.Sender.IsStreaming = false;
        h.Clock.OnDelay = _ => { if (h.Clock.Milliseconds == 11000) h.Clipboard.SetText("cloud item"); };
        var result = h.Run(new GameAction.CopyItem());
        Assert.Equal(ActionOutcome.Success, result.Outcome);
        Assert.Equal("cloud item", result.CopiedText);
        Assert.Equal(500, h.Clipboard.LastReadBudget);
        Assert.Equal(11000, h.Clock.Milliseconds);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
    }
    [Fact]
    public void CopyReadUsesRemainingDeadlineAndReportsTimeout()
    {
        var h = new ActionHarness();
        h.Sender.IsStreaming = true;
        h.Clock.OnDelay = _ => { if (h.Clock.Milliseconds == 11000) h.Clipboard.SetText("delayed provider"); };
        h.Clipboard.ReadWithBudget = budget => { h.Clock.Delay(budget); throw new TimeoutException(); };
        var result = h.Run(new GameAction.CopyItem());
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Equal("Copy timed out reading clipboard text", result.Reason);
        Assert.Equal(500, h.Clipboard.LastReadBudget);
        Assert.Equal(11500, h.Clock.Milliseconds);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
    }
    [Fact]
    public void NativeReaderTimeoutIsNotHiddenByAggregateException()
    {
        var reader = new BoundedClipboardRead<string?>();
        Assert.Throws<TimeoutException>(() => reader.Read(_ => throw new TimeoutException("Clipboard open timed out")));
    }
    [Fact]
    public void StaleBoundPressProducesDiagnosticWithoutIssuingOrBlocking()
    {
        var dispatcher = new HotkeyDispatcher(new IntentRegistry(new TestClock()),
            [new("copy", 118, 0, true, new GameAction.CopyItem())]);
        var notice = new StaleFocusNotice();
        var lease = new ForegroundLease();
        lease.Publish(123, true, 10000);
        Assert.True(lease.IsStale(10251));
        var key = new PhysicalKey(118, true, false, 0, 10251);
        Assert.True(notice.RecordCandidate(dispatcher, key));
        var decision = dispatcher.Observe(key, true, passThrough: true);
        Assert.False(decision.Block);
        Assert.Null(decision.Press);
        Assert.Equal("copy", notice.Take()!.Id);
        Assert.Equal("hotkey ignored: focus state stale", StaleFocusNotice.Reason);
        Assert.False(notice.RecordCandidate(dispatcher, key)); // no repeated diagnostic for held keys
        Assert.Null(notice.Take());
    }
}
