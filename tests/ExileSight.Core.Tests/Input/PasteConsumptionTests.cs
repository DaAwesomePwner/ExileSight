using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class PasteConsumptionTests
{
    [Theory]
    [InlineData(1, 750)]
    [InlineData(2, 750)]
    [InlineData(151, 750)]
    public void NativeRestorationNeverPrecedesMinimumHold(int pumps, int elapsed)
    {
        var h = new ActionHarness();
        h.Clipboard.ConsumeAfterPumps = pumps;
        var result = h.Run(new GameAction.SendChatCommand("/hideout"));
        Assert.Equal(ActionOutcome.Success, result.Outcome);
        Assert.Equal("Sent (paste confirmed)", result.PasteStatus);
        Assert.Equal(10000 + elapsed, h.Clock.Milliseconds);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
        Assert.Equal(1, result.FormatsPreserved);
    }
    [Fact]
    public void MissingOrNonGameConsumptionReportsSentWithoutExtraStall()
    {
        var h = new ActionHarness();
        h.Clipboard.ConsumeAfterPumps = int.MaxValue;
        var action = new GameAction.SendChatCommand("/hideout");
        var token = h.Token(action);
        var result = h.Gate.TryExecute(token, action);
        Assert.Equal(ActionOutcome.Success, result.Outcome);
        Assert.Equal("Sent (unconfirmed)", result.PasteStatus);
        Assert.Equal(10750, h.Clock.Milliseconds);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, h.Gate.TryExecute(token, action).Outcome);
        Assert.Equal(1, h.Sender.Calls);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SearchAndPriceNotePreserveEverySnapshotFormat(bool price)
    {
        var h = new ActionHarness();
        h.Clipboard.ExtraFormats.Add(49152, [1, 2, 3]);
        h.Clipboard.SkippedFormats = [2];
        GameAction action = price ? new GameAction.SetPriceNote("~b/o 1 divine") : new GameAction.PasteSearch("life");
        var result = h.Run(action);
        Assert.Equal(ActionOutcome.Success, result.Outcome);
        Assert.Equal("Sent (paste confirmed)", result.PasteStatus);
        Assert.Equal(2, result.FormatsPreserved);
        Assert.Equal(1, result.FormatsSkipped);
        Assert.Equal(new byte[] { 1, 2, 3 }, h.Clipboard.ExtraFormats[49152]);
        Assert.Equal(1, h.Sender.Calls);
        Assert.Single(h.Audit.Lines);
    }

    [Theory]
    [InlineData(1, "Sent (paste confirmed)")]
    [InlineData(int.MaxValue, "Sent (unconfirmed)")]
    public void StreamingAlwaysHoldsForFullFifteenHundredMilliseconds(int pumps, string status)
    {
        var h = new ActionHarness();
        h.Sender.IsStreaming = true;
        h.Clipboard.ConsumeAfterPumps = pumps;
        h.Clock.OnDelay = _ => Assert.Equal("/hideout", h.Clipboard.Text);
        var result = h.Run(new GameAction.SendChatCommand("/hideout"));
        Assert.Equal(ActionOutcome.Success, result.Outcome);
        Assert.Equal(status, result.PasteStatus);
        Assert.Equal(11500, h.Clock.Milliseconds);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
    }

    [Fact]
    public void IssuancePrimitiveAndRetainedCapabilityArePrivate()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Assert.Null(typeof(IntentRegistry).GetMethod("CreateHookIssuer", flags));
        var registry = new IntentRegistry(new TestClock(), new object());
        Assert.Throws<InvalidOperationException>(() => registry.Register(new object(), 0, "fake", ActionKind.CopyItem));
        Assert.True(typeof(HotkeyDispatcher).GetField("issue", flags)!.IsPrivate);
        Assert.DoesNotContain(typeof(IntentRegistry).GetMethods(), method => method.ReturnType == typeof(IntentToken));
    }
}
