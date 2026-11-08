using System.Globalization;
using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class ListingReviewTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "listing-review-" + Guid.NewGuid());
    private readonly List<GameAction> sent = [];
    private JsonElement? setting;
    private static ParsedItem Item(string? amount) => ItemTextParser.Parse(
        "Item Class: Rings\nRarity: Normal\nIron Ring\n--------\n" +
        (amount is null ? "" : "Note: ~price " + amount + " divine"), Game.PoE1).Item!;
    private ShopModel Open(string? amount, bool notes = false, decimal? seed = null)
    {
        setting = JsonSerializer.SerializeToElement(notes ? "note" : "async");
        var model = new ShopModel(new(directory), () => "Standard")
        { SeedPrice = (_, _) => Task.FromResult(seed is null ? null : new ShopNote("~b/o", seed.Value, "chaos")) };
        _ = new FeatureHost([new VaalasyncFeature(model)], (_, _) => setting, (_, _, value) => setting = value,
            (_, _, action) => { sent.Add(action); return new(ActionOutcome.Success); });
        model.Handle(Item(amount), new(Game.PoE1), OmniPressKind.Short);
        return model;
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnpricedHasOnlyCustomAndOneHistoryEntryPerSend(bool seeded)
    {
        var model = Open(null, seed: seeded ? 4.5m : null);
        Assert.Null(model.Note);
        Assert.Contains(seeded ? "poe.ninja estimate" : "No current price", model.BaselineDescription);
        foreach (var choice in new[] { "minus1", "plus1", "minus10", "plus10" }) Assert.Null(model.Suggest(choice));
        Assert.Equal(seeded ? "5" : "", model.Custom);
        model.Custom = "7";
        model.Apply("custom", new());
        var row = Assert.Single(model.History.Snapshot());
        Assert.Equal("Sent", Assert.Single(row.Prices).Kind);
        model.Handle(Item(null), new(Game.PoE1), OmniPressKind.Short);
        Assert.Single(Assert.Single(model.History.Snapshot()).Prices);
        model.Custom = "8";
        model.Apply("custom", new());
        Assert.Equal(new[] { "Sent", "Sent" }, Assert.Single(model.History.Snapshot()).Prices.Select(p => p.Kind));
    }
    [Fact]
    public void AsyncRoundsClampsAndDropsDuplicateSuggestions()
    {
        var model = Open("1.5");
        var suggestions = new[] { "minus1", "plus1", "minus10", "plus10" }.Select(model.Suggest).Where(n => n is not null).ToArray();
        Assert.Equal(new decimal[] { 1, 3, 2 }, suggestions.Select(n => n!.Amount));
        Assert.All(suggestions, n => Assert.Equal(decimal.Truncate(n!.Amount), n.Amount));
    }
    [Theory]
    [InlineData("4.5")]
    [InlineData("9/2")]
    public void AsyncRefusesFractionalCustomBeforeInput(string custom)
    {
        var model = Open("10");
        model.Custom = custom;
        Assert.Null(model.Suggest("custom"));
        Assert.Equal(ActionOutcome.Failed, model.Apply("custom", new()).Outcome);
        Assert.Contains("whole number", model.Status);
        Assert.Empty(sent);
        Assert.True(model.CanApply);
        Assert.Contains("whole number", model.SaveCustom(custom, "divine"));
    }
    [Fact]
    public void NoteModeKeepsDecimalsAndDisplayIsInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var model = Open("5", notes: true);
            Assert.Null(model.SaveCustom("4.5", " divine "));
            Assert.Equal("divine", model.Currency);
            Assert.Equal("4.5 divine", model.Suggest("custom")!.DisplayAmount);
            model.Apply("custom", new());
            Assert.Equal("~price 4.5 divine", Assert.IsType<GameAction.SetPriceNote>(Assert.Single(sent)).Text);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Fact]
    public void ReminderAndSavedModeReflectActualState()
    {
        var model = Open("5");
        Assert.Equal("", model.CurrencyReminder);
        Assert.Equal(model.Status, model.DisplayStatus);
        model.Currency = "chaos";
        Assert.Contains("chaos", model.CurrencyReminder);
        model.Currency = "divine";
        Assert.Equal("", model.CurrencyReminder);
        Assert.Equal("Invalid currency.", model.SaveCustom("5", "not currency"));
        Assert.False(model.SavedNoteMode);
        model.SaveListingMode(true);
        Assert.True(model.SavedNoteMode);
        Assert.False(model.NoteMode);
        Assert.Contains("applies after restart", model.ListingModeLabel);
        model.SaveListingMode(false);
        Assert.DoesNotContain("applies after restart", model.ListingModeLabel);
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
