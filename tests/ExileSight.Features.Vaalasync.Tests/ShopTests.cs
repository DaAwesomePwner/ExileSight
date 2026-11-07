using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Core.Logs;
using ExileSight.Features.Vaalasync;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class ShopTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "vaalasync-" + Guid.NewGuid());
    [Fact]
    public void FixturesAndFractions()
    {
        var notes = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "notes.json")))!;
        Assert.All(notes, text => Assert.NotNull(ShopNote.Parse(text)));
        Assert.Equal(.5m, ShopNote.Parse(notes[0])!.Amount);
        Assert.Equal("~price 12.5 chaos", ShopNote.Parse(notes[1])!.ToString());
    }
    [Theory]
    [InlineData("~b/o 0 chaos")]
    [InlineData("~price -1 chaos")]
    [InlineData("~b/o NaN chaos")]
    [InlineData("~b/o 1000001 chaos")]
    [InlineData("~b/o 1/0 chaos")]
    [InlineData("~b/o 1 chaos\n/run")]
    [InlineData("~b/o 1/0.0000001 chaos")]
    [InlineData("~b/o 1000000/0.0000000000000000000000000001 chaos")]
    public void RejectInvalid(string text) => Assert.Null(ShopNote.Parse(text));
    [Fact]
    public void SuggestionsPreservePrefixAndRejectNonpositive()
    {
        var note = ShopNote.Parse("~price 10 divine")!;
        Assert.Equal(9, note.Suggest("minus1")!.Amount);
        Assert.Equal(11, note.Suggest("plus10")!.Amount);
        Assert.Equal("~price 0.5 divine", note.Suggest("custom", "1/2")!.ToString());
        Assert.Null((note with { Amount = .5m }).Suggest("minus1"));
        Assert.Equal(9, note.Suggest("minus10")!.Amount);
        Assert.Equal(11, note.Suggest("plus1")!.Amount);
    }
    [Fact]
    public void CommandsSendOnceAndRefuseMissingOrUnregisteredTokens()
    {
        var model = new ShopModel(new(directory), () => "Standard");
        var accepted = new IntentToken(); var sent = new List<GameAction>();
        var host = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { },
            (_, token, action) =>
            {
                if (!ReferenceEquals(token, accepted)) return new(ActionOutcome.UsedOrUnknownToken);
                accepted = new(); sent.Add(action); return new(ActionOutcome.Success);
            });
        var item = ItemTextParser.Parse("Item Class: Rings\nRarity: Rare\nTest Ring\nIron Ring\n--------\nItem Level: 80\n--------\nNote: ~price 2 divine", Game.PoE1).Item!;
        Assert.True(host.Route(item, new(Game.PoE1), OmniPressKind.Short));
        var command = host.Commands.Single(c => c.Command == "vaalasync:plus1");
        Assert.Equal(ActionOutcome.MissingToken, command.Execute().Outcome);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, command.Execute(new()).Outcome);
        Assert.Empty(sent);
        var physical = accepted;
        Assert.Equal(ActionOutcome.Success, command.Execute(physical).Outcome);
        Assert.Equal(ActionOutcome.Failed, command.Execute(physical).Outcome);
        Assert.False(model.CanApply);
        Assert.Contains(ShopModel.RearmMessage, model.Status);
        Assert.Equal("~price 3 divine", Assert.IsType<GameAction.SetPriceNote>(Assert.Single(sent)).Text);
        Assert.Equal(ActionOutcome.Failed, command.Execute(accepted).Outcome);
        host.Route(item, new(Game.PoE1), OmniPressKind.Short);
        Assert.True(model.CanApply);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, command.Execute(physical).Outcome);
        Assert.Equal(ActionOutcome.Success, command.Execute(accepted).Outcome);
        Assert.Equal(2, sent.Count);
    }
    [Fact]
    public void HistoryRoundTripsBoundsAndWhispersNeverStoreBuyerOrMarkSold()
    {
        var store = new HistoryStore(directory); var now = DateTimeOffset.UtcNow;
        var note = ShopNote.Parse("~b/o 2 divine")!;
        var id = store.Observe("Test Ring", "Iron Ring", 80, "Standard", note, now);
        for (int i = 0; i < 60; i++) store.Change(id, note, now);
        store.Evidence(new TradeWhisper("SECRET RAW", null, "@From", "SECRET BUYER", "SECRET TEXT", "Test Ring", "2 divine", "Standard", "SECRET STASH"), now);
        var restored = new HistoryStore(directory).Snapshot();
        Assert.Equal(HistoryStore.MaxPrices, restored[0].Prices.Length);
        Assert.Null(restored[0].Sold); Assert.NotNull(restored[0].Evidence);
        Assert.DoesNotContain("SECRET", File.ReadAllText(Path.Combine(directory, "history.json")));
        store.MarkSold(id, now); Assert.NotNull(new HistoryStore(directory).Snapshot()[0].Sold);
        for (int i = 0; i < 505; i++) store.RecordPurchase("Item " + i, "Standard", note, now);
        Assert.Equal(HistoryStore.MaxListings, new HistoryStore(directory).Snapshot().Length);
    }
    [Fact]
    public void UnwritableHistoryDisablesPriceActions()
    {
        var store = new HistoryStore(directory);
        File.WriteAllText(directory, "blocks directory creation");
        try
        {
            var model = new ShopModel(store, () => "Standard");
            var sent = 0;
            var host = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { },
                (_, _, _) => { sent++; return new(ActionOutcome.Success); });
            var item = ItemTextParser.Parse("Item Class: Rings\nRarity: Normal\nIron Ring\n--------\nNote: ~b/o 2 divine", Game.PoE1).Item!;
            host.Route(item, new(Game.PoE1), OmniPressKind.Short);
            Assert.Null(model.Note);
            Assert.Equal(ActionOutcome.Failed, host.Commands[0].Execute(new()).Outcome);
            Assert.Equal(0, sent);
        }
        finally { File.Delete(directory); }
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
