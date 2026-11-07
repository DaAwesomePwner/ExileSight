using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class PasteConsumptionTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 5)]
    [InlineData(301, 1500)]
    public void RestorationFollowsConsumptionNotFixedRetention(int pumps, int elapsed)
    {
        var h = new ActionHarness();
        h.Clipboard.ConsumeAfterPumps = pumps;
        var result = h.Run(new GameAction.SendChatCommand("/hideout"));
        Assert.Equal(ActionOutcome.Success, result.Outcome);
        Assert.Equal("Consumed", result.PasteStatus);
        Assert.Equal(10000 + elapsed, h.Clock.Milliseconds);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
        Assert.Equal(1, result.FormatsPreserved);
    }
    [Fact]
    public void MissingConsumptionTimesOutRestoresAndConsumesToken()
    {
        var h = new ActionHarness();
        h.Clipboard.ConsumeAfterPumps = int.MaxValue;
        var action = new GameAction.SendChatCommand("/hideout");
        var token = h.Token(action);
        var result = h.Gate.TryExecute(token, action);
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Equal("Timed out", result.PasteStatus);
        Assert.Equal(11500, h.Clock.Milliseconds);
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
        Assert.Equal("Consumed", result.PasteStatus);
        Assert.Equal(2, result.FormatsPreserved);
        Assert.Equal(1, result.FormatsSkipped);
        Assert.Equal(new byte[] { 1, 2, 3 }, h.Clipboard.ExtraFormats[49152]);
        Assert.Equal(1, h.Sender.Calls);
        Assert.Single(h.Audit.Lines);
    }

    [Fact]
    public void IssuancePrimitiveAndRetainedCapabilityArePrivate()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Assert.True(typeof(IntentRegistry).GetMethod("Issue", flags)!.IsPrivate);
        Assert.True(typeof(HotkeyDispatcher).GetField("issue", flags)!.IsPrivate);
        Assert.DoesNotContain(typeof(IntentRegistry).GetMethods(), method => method.ReturnType == typeof(IntentToken));
    }
}
