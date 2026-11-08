using System.Globalization;
using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Xunit;
namespace ExileSight.Core.Tests.Input;

public sealed class ListingAmountTests
{
    [Fact]
    public void AmountUsesInvariantTextAndNeverSubmits()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal("1234.5", new GameAction.SetListingAmount(1234.5m).Text);
            Assert.Equal(new KeyStroke[] { new(17,true), new(65,true), new(65,false),
                new(86,true), new(86,false), new(17,false) }, ActionData.Sequence(new GameAction.SetListingAmount(90)));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
    [Fact]
    public void GatePastesOnceRestoresClipboardAndAuditsTruthfully()
    {
        var h = new ActionHarness();
        var action = new GameAction.SetListingAmount(90);
        var token = h.Token(action);
        h.Sender.OnSend = () => Assert.Equal("90", h.Clipboard.Text);
        Assert.Equal(ActionOutcome.Success, h.Gate.TryExecute(token, action).Outcome);
        Assert.Equal("private user clipboard", h.Clipboard.Text);
        Assert.Equal(1, h.Sender.Calls);
        Assert.DoesNotContain(h.Sender.LastSequence!, k => k.Key == 13);
        Assert.Contains("SetListingAmount", Assert.Single(h.Audit.Lines));
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, h.Gate.TryExecute(token, action).Outcome);
        Assert.Equal(1, h.Sender.Calls);
    }
    [Fact]
    public void GateRefusesAmountWhenGameIsNotForeground()
    {
        var h = new ActionHarness();
        h.Sender.IsGameForeground = false;
        Assert.Equal(ActionOutcome.NotForeground, h.Run(new GameAction.SetListingAmount(90)).Outcome);
        Assert.Equal(0, h.Sender.Calls);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidAmountsAreRejectedBeforeInput(int amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameAction.SetListingAmount(amount));
}
