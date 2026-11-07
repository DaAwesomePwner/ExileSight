using ExileSight.Core.Input;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class ReviewSafeguardTests
{
    [Theory]
    [InlineData(ActionKind.PasteSearch)]
    [InlineData(ActionKind.SetPriceNote)]
    public void PhaseTwoActionsAreRejectedByUserBindingMapper(ActionKind action)
    {
        var result = BindingMapper.Map([new PersistedBinding { Id = "future", Key = 118, Action = action, Text = "life" }], new(), []);
        Assert.Empty(result.Bindings);
        Assert.Contains("Phase 2", Assert.Single(result.Rejected));
    }
    [Fact]
    public void FutureSequencesMatchFairPlayExactly()
    {
        Assert.Equal(new KeyStroke[] { new(17,true),new(70,true),new(70,false),new(65,true),new(65,false),
            new(86,true),new(86,false),new(17,false),new(13,true),new(13,false) },
            ActionData.Sequence(new GameAction.PasteSearch("life")));
        Assert.Equal(new KeyStroke[] { new(17,true),new(65,true),new(65,false),new(86,true),new(86,false),
            new(17,false),new(13,true),new(13,false) }, ActionData.Sequence(new GameAction.SetPriceNote("note")));
    }
    [Fact]
    public void UiThreadRefusalConsumesAndAuditsWithoutClipboardOrPump()
    {
        var h = new ActionHarness();
        h.Sender.CanExecuteOnCurrentThread = false;
        h.Clipboard.SaveFails = true;
        var action = new GameAction.SendChatCommand("/hideout");
        var token = h.Token(action);
        var result = h.Gate.TryExecute(token, action);
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Contains("dedicated worker", result.Reason);
        Assert.Equal(10000, h.Clock.Milliseconds);
        Assert.Equal(0, h.Sender.Calls);
        Assert.Equal(0, h.Clipboard.RestoreCalls);
        Assert.Single(h.Audit.Lines);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, h.Gate.TryExecute(token, action).Outcome);
    }
    [Fact]
    public void StaleOrChangedForegroundPassesEvenPreviouslyBlockedRepeatAndRelease()
    {
        var lease = new ForegroundLease();
        lease.Publish(123, true, 10000);
        Assert.True(lease.AllowsBlocking(10250, 123));
        Assert.False(lease.AllowsBlocking(10251, 123));
        Assert.False(lease.AllowsBlocking(10001, 456));
        var dispatcher = new HotkeyDispatcher(new IntentRegistry(new TestClock()),
            [new("copy", 118, 0, true, new GameAction.CopyItem())]);
        Assert.True(dispatcher.Observe(new(118, true, false, 0, 10000), true).Block);
        var repeat = dispatcher.Observe(new(118, true, false, 0, 10251), true, !lease.AllowsBlocking(10251, 123));
        Assert.False(repeat.Block);
        Assert.Null(repeat.Press);
        Assert.False(dispatcher.Observe(new(118, false, false, 0, 10260), true, true).Block);
        Assert.Null(dispatcher.Observe(new(118, true, false, 0, 10270), true, true).Press);
    }
    [Fact]
    public void PartialUserRecoveryIsNotClearedWhenActionTextWasAlreadyRemoved()
    {
        var h = new ActionHarness();
        h.Clipboard.RestoreFailures = 3;
        h.Clock.OnDelay = _ =>
        {
            if (h.Clipboard.RestoreCalls > 0)
            {
                h.Clipboard.Text = "recovered user text";
                h.Clipboard.RecoveredFormats = 1;
            }
        };
        var result = h.Run(new GameAction.SendChatCommand("/hideout"));
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Contains("1 user formats recovered; action text removed", result.Reason);
        Assert.Equal("recovered user text", h.Clipboard.Text);
        Assert.Equal(0, h.Clipboard.ClearCalls);
    }
    [Fact]
    public void SlowSaveReturnsWithinDeadlineAndAllowsOnlyOneReadUntilProviderFinishes()
    {
        using var release = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        var reader = new BoundedClipboardRead<int>();
        try
        {
            Assert.Throws<TimeoutException>(() => reader.Read(token =>
            {
                try { release.Wait(); token.ThrowIfCancellationRequested(); return 1; }
                finally { finished.Set(); }
            }, 20));
            Assert.Throws<TimeoutException>(() => reader.Read(_ => throw new InvalidOperationException("Second provider started"), 20));
        }
        finally { release.Set(); Assert.True(finished.Wait(5000)); }
    }
}
