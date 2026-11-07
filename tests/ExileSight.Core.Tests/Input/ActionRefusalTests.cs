using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class ActionRefusalTests
{
    private static readonly GameAction Copy = new GameAction.CopyItem();
    private static readonly GameAction Chat = new GameAction.SendChatCommand("/hideout");
    [Fact]
    public void BindingCannotAuthorizeAnotherActionKindAndMismatchConsumesToken()
    {
        var h = new ActionHarness();
        var token = h.Token(Copy);
        Assert.Equal(ActionOutcome.WrongAction, h.Gate.TryExecute(token, Chat).Outcome);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, h.Gate.TryExecute(token, Copy).Outcome);
        Assert.Equal(0, h.Sender.Calls);
    }
    [Fact]
    public void SaveFailureDoesNotMutateClipboardOrSend()
    {
        var h = new ActionHarness();
        h.Clipboard.SaveFails = true;
        Assert.Equal("Clipboard could not be preserved", h.Run(Copy).Reason);
        Assert.Equal(0, h.Sender.Calls);
        Assert.Equal(0, h.Clipboard.RestoreCalls);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
    }
    [Fact]
    public void SetFailureStillRestores()
    {
        var h = new ActionHarness();
        h.Clipboard.SetFails = true;
        Assert.Equal(ActionOutcome.Failed, h.Run(Chat).Outcome);
        Assert.Equal(1, h.Clipboard.RestoreCalls);
        Assert.Equal(0, h.Sender.Calls);
    }
    [Fact]
    public void ReadFailureStillRestores()
    {
        var h = new ActionHarness();
        h.Sender.OnSend = () => h.Clipboard.SetText("item");
        h.Clipboard.ReadFails = true;
        Assert.Equal(ActionOutcome.Failed, h.Run(Copy).Outcome);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
    }
    [Fact]
    public void LossOfForegroundAfterClipboardPreparationRefusesAndRestores()
    {
        var h = new ActionHarness();
        h.Clipboard.BeforeSet = () => h.Sender.IsGameForeground = false;
        Assert.Equal(ActionOutcome.NotForeground, h.Run(Chat).Outcome);
        Assert.Equal(0, h.Sender.Calls);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
    }
    [Fact]
    public void ExpiryDuringPreparationRefusesAndRestores()
    {
        var h = new ActionHarness();
        h.Clipboard.BeforeSet = () => h.Clock.Delay(1500);
        Assert.Equal(ActionOutcome.Expired, h.Run(Chat).Outcome);
        Assert.Equal(0, h.Sender.Calls);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
    }
    [Fact]
    public void HeldModifierRefusesWithoutSending()
    {
        var h = new ActionHarness();
        h.Sender.Modifiers = KeyModifiers.Shift;
        var result = h.Run(Chat);
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Contains("Release modifiers", result.Reason);
        Assert.Equal(0, h.Sender.Calls);
        Assert.Equal(11400, h.Clock.Milliseconds);
    }
    [Fact]
    public void RealModifierReleaseAllowsAction()
    {
        var h = new ActionHarness();
        h.Sender.Modifiers = KeyModifiers.Shift;
        h.Clock.OnDelay = _ => h.Sender.Modifiers = KeyModifiers.None;
        Assert.Equal(ActionOutcome.Success, h.Run(Chat).Outcome);
        Assert.Equal(1, h.Sender.Calls);
    }
    [Fact]
    public void ModifierChangeDuringPreparationRefuses()
    {
        var h = new ActionHarness();
        h.Clipboard.BeforeSet = () => h.Sender.Modifiers = KeyModifiers.Control;
        Assert.Equal(ActionOutcome.Failed, h.Run(Chat).Outcome);
        Assert.Equal(0, h.Sender.Calls);
    }
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/hideout\n/kills")]
    public void InvalidChatDoesNotSend(string text)
    {
        var h = new ActionHarness();
        Assert.Equal(ActionOutcome.Failed, h.Run(new GameAction.SendChatCommand(text)).Outcome);
        Assert.Equal(0, h.Sender.Calls);
    }
}
