using ExileSight.Features.Vaalasync;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class ShopCardMappingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static ShopCardInput Input() => new("Ring", new("~b/o", 10, "chaos"), Now.AddHours(-3),
        "chaos", true, false, "Open amount box", "Open amount box", "",
        new Dictionary<string, ShopNote?>
        {
            ["minus10"] = null, ["minus1"] = new("~b/o", 9, "chaos"),
            ["plus1"] = new("~b/o", 11, "chaos"), ["plus10"] = null
        }, null);

    [Fact]
    public void DuplicatesShowWholeAmountsButStayDisabled()
    {
        var card = ShopCardMapping.Map(Input(), Now, null);
        Assert.Equal(new[] { "9", "9", "11", "11" }, card.Amounts.Select(a => a.Text));
        Assert.Equal(new[] { false, true, true, false }, card.Amounts.Select(a => a.Available));
        Assert.Equal(TimeSpan.FromHours(3), card.ListedFor);
        Assert.Equal("", card.Notice);
        var rounded = ShopCardMapping.Map(Input() with { Note = new("~b/o", 25, "chaos") }, Now, null);
        Assert.Equal("23", rounded.Amounts[0].Text);
        Assert.Equal("28", rounded.Amounts[3].Text);
    }

    [Fact]
    public void SuccessfulPasteRetainsCurrencyReminderWithoutRedundantStatus()
    {
        var input = Input() with { Armed = false, Status = ShopModel.RearmMessage,
            CurrencyReminder = "Switch the game’s currency dropdown to divine." };
        var card = ShopCardMapping.Map(input, Now, "11");
        Assert.Equal("11", card.Pasted);
        Assert.Equal(input.CurrencyReminder, card.Notice);
        var diagnostic = ShopCardMapping.Map(input with { Status = "Recovery notice\n" + ShopModel.RearmMessage + " — Sent; clipboard restored" }, Now, "11");
        Assert.Contains("Recovery notice", diagnostic.Notice);
        Assert.Contains("clipboard restored", diagnostic.Notice);
        Assert.DoesNotContain(ShopModel.RearmMessage, diagnostic.Notice);
        Assert.DoesNotContain("— Sent", diagnostic.Notice);
        Assert.Contains("Sent; clipboard restored", diagnostic.Notice);
    }

    [Theory]
    [InlineData(true, "NotForeground")]
    [InlineData(false, "Price possibly set")]
    public void RefusedOrUncertainSendsNeverClaimPasted(bool armed, string status)
    {
        var card = ShopCardMapping.Map(Input() with { Armed = armed, Status = status }, Now, "11");
        Assert.Null(card.Pasted);
        Assert.Equal(status, card.Notice);
    }

    [Fact]
    public void NoteAndCustomKeepFullTextAndMissingTimestampIsOmitted()
    {
        var input = Input() with { NoteMode = true, FirstListedAt = null, Custom = new("~price", .5m, "divine") };
        var card = ShopCardMapping.Map(input, Now, null);
        Assert.Equal("~b/o 9 chaos", card.Amounts[1].Text);
        Assert.Equal("—", card.Amounts[0].Text);
        Assert.Equal("~price 0.5 divine", card.CustomAmount);
        Assert.Null(card.ListedFor);
    }
}
