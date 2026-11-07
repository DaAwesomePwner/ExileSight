using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class ActionExecutorTests
{
    private static readonly GameAction Copy = new GameAction.CopyItem();
    private static readonly GameAction Chat = new GameAction.SendChatCommand("/w Person confidential");
    [Fact]
    public void CopyReturnsTextAndRestoresSnapshot()
    {
        var h = new ActionHarness();
        h.Sender.OnSend = () => h.Clipboard.SetText("copied item");
        var result = h.Run(Copy);
        Assert.Equal(ActionOutcome.Success, result.Outcome);
        Assert.Equal("copied item", result.CopiedText);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
        Assert.Equal(1, h.Gate.ActionsSent);
        Assert.Single(h.Audit.Lines);
    }
    [Fact]
    public void CopyTimeoutRestoresAndConsumes()
    {
        var h = new ActionHarness();
        var token = h.Token(Copy);
        var result = h.Gate.TryExecute(token, Copy);
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Equal("Copy timed out", result.Reason);
        Assert.Equal(10500, h.Clock.Milliseconds);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, h.Gate.TryExecute(token, Copy).Outcome);
        Assert.Equal(1, h.Sender.Calls);
    }
    [Fact]
    public void ChatRetainsPayloadForBoundedWaitAndUsesOneUnchangedSequence()
    {
        var h = new ActionHarness();
        h.Clock.OnDelay = _ => Assert.Equal("/w Person confidential", h.Clipboard.Text);
        Assert.Equal(ActionOutcome.Success, h.Run(Chat).Outcome);
        Assert.Equal(10750, h.Clock.Milliseconds);
        Assert.Equal(1, h.Sender.Calls);
        Assert.Equal(8, h.Sender.LastSequence!.Count);
        Assert.DoesNotContain(h.Sender.LastSequence, k => k.Key == 65); // no Ctrl+A
        Assert.Equal("private user clipboard", h.Clipboard.Text);
        Assert.DoesNotContain("confidential", Assert.Single(h.Audit.Lines));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void PartialOrBlockedSendFailsAndRestores(uint inserted)
    {
        var h = new ActionHarness();
        h.Sender.Result = new(false, inserted, "SendInput inserted fewer events; error 5; cleanup complete");
        var result = h.Run(Chat);
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Contains("error 5", result.Reason);
        Assert.Contains("error 5", Assert.Single(h.Audit.Lines));
        Assert.Equal("private user clipboard", h.Clipboard.Text);
        Assert.Equal(inserted == 0 ? 10000 : 10750, h.Clock.Milliseconds);
    }
    [Fact]
    public void AuditFailureKeepsCopySuccessAndPayload()
    {
        var h = new ActionHarness();
        h.Sender.OnSend = () => h.Clipboard.SetText("item");
        h.Audit.AppendFails = true;
        var result = h.Run(Copy);
        Assert.Equal(ActionOutcome.Success, result.Outcome);
        Assert.Equal("item", result.CopiedText);
        Assert.True(result.AuditFailed);
    }
    [Fact]
    public void AuditFailureKeepsChatSuccess()
    {
        var h = new ActionHarness();
        h.Audit.AppendFails = true;
        var result = h.Run(Chat);
        Assert.Equal(ActionOutcome.Success, result.Outcome);
        Assert.True(result.AuditFailed);
        Assert.Equal(1, h.Sender.Calls);
    }
    [Fact]
    public void UnavailableAuditBeforeSendingRefuses()
    {
        var h = new ActionHarness();
        h.Audit.PrepareFails = true;
        var result = h.Run(Chat);
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.True(result.AuditFailed);
        Assert.Equal(0, h.Sender.Calls);
        Assert.Equal(0, h.Clipboard.RestoreCalls);
    }
    [Fact]
    public void RestoreRetriesBeforeReturningSuccess()
    {
        var h = new ActionHarness();
        h.Clipboard.RestoreFailures = 2;
        Assert.Equal(ActionOutcome.Success, h.Run(Chat).Outcome);
        Assert.Equal(3, h.Clipboard.RestoreCalls);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ExhaustedRestoreClearsPayloadAndAuditsFailure(int clearFailures)
    {
        var h = new ActionHarness();
        h.Clipboard.RestoreFailures = 3;
        h.Clipboard.ClearFailures = clearFailures;
        var result = h.Run(Chat);
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Contains("clipboard cleared", result.Reason);
        Assert.Null(h.Clipboard.Text);
        Assert.Equal(3, h.Clipboard.RestoreCalls);
        Assert.Equal(clearFailures + 1, h.Clipboard.ClearCalls);
        Assert.Contains("clipboard cleared", Assert.Single(h.Audit.Lines));
    }
    [Fact]
    public void ImpossibleRestoreAndClearAreReportedAfterBoundedRetries()
    {
        var h = new ActionHarness();
        h.Clipboard.RestoreFailures = h.Clipboard.ClearFailures = 100;
        var result = h.Run(Chat);
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Contains("restore and clear failed", result.Reason);
        Assert.Equal(3, h.Clipboard.RestoreCalls);
        Assert.Equal(3, h.Clipboard.ClearCalls);
        Assert.Equal(10850, h.Clock.Milliseconds);
    }
}
