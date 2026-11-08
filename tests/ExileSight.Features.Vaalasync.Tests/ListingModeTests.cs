using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class ListingModeTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "listing-" + Guid.NewGuid());
    private static ParsedItem Item(string note = "") => ItemTextParser.Parse(
        "Item Class: Rings\nRarity: Normal\nIron Ring\n--------\n" + note, Game.PoE1).Item!;
    [Theory]
    [InlineData("async", false)]
    [InlineData("note", true)]
    [InlineData("invalid", false)]
    public void ModeSelectsDeclaredActionAndDisarms(string mode, bool notes)
    {
        var model = new ShopModel(new(directory), () => "Standard");
        var sent = new List<GameAction>();
        var host = new FeatureHost([new VaalasyncFeature(model)], (_, _) => JsonSerializer.SerializeToElement(mode),
            (_, _, _) => { }, (_, _, action) => { sent.Add(action); return new(ActionOutcome.Success); });
        host.Route(Item("Note: ~price 90 divine"), new(Game.PoE1), OmniPressKind.Short);
        Assert.All(host.Commands, c => Assert.Equal(notes ? ActionKind.SetPriceNote : ActionKind.SetListingAmount, c.RequiredAction));
        model.Apply("plus1", new());
        Assert.Equal(notes ? ActionKind.SetPriceNote : ActionKind.SetListingAmount, Assert.Single(sent).Kind);
        if (notes) Assert.Equal("~price 91 divine", Assert.IsType<GameAction.SetPriceNote>(sent[0]).Text);
        else Assert.Equal("91", Assert.IsType<GameAction.SetListingAmount>(sent[0]).Text);
        Assert.False(model.CanApply);
        model.Apply("plus1", new());
        Assert.Single(sent);
    }
    [Fact]
    public void UnpricedMenuSeedsCustomAndDoesNotSendOrRecordUntilClick()
    {
        var model = new ShopModel(new(directory), () => "Standard")
        { SeedPrice = (_, _) => Task.FromResult<ShopNote?>(new("~b/o", 12, "chaos")) };
        var probe = new MenuProbe();
        var calls = 0;
        var host = new FeatureHost([new VaalasyncFeature(model), probe], (_, _) => null, (_, _, _) => { },
            (_, _, action) => { calls++; Assert.Equal("12", Assert.IsType<GameAction.SetListingAmount>(action).Text); return new(ActionOutcome.Success); });
        Assert.False(host.Route(Item(), new(Game.PoE1), OmniPressKind.Short));
        var entry = Assert.Single(ItemMenuEntries.Build(Item(), probe.Providers));
        Assert.Equal("Set listing price…", entry.Label);
        entry.Execute();
        Assert.Equal("12", model.Custom);
        Assert.Equal(0, calls);
        Assert.Empty(model.History.Snapshot());
        Assert.Contains("dropdown", model.CurrencyReminder);
        model.Apply("custom", new());
        Assert.Equal(1, calls);
        Assert.Single(model.History.Snapshot());
        Assert.False(model.CanApply);
    }
    [Fact]
    public async Task LateSeedCannotOverwriteCustomEdit()
    {
        var completion = new TaskCompletionSource<ShopNote?>();
        var model = new ShopModel(new(directory), () => "Standard") { SeedPrice = (_, _) => completion.Task };
        _ = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { });
        Assert.Single(model.BuildMenu(Item())).Execute();
        model.Custom = "77";
        completion.SetResult(new("~b/o", 12, "divine"));
        await completion.Task;
        Assert.Equal("77", model.Custom);
        Assert.Equal("chaos", model.Currency);
    }
    private sealed class MenuProbe : IFeature
    {
        public IReadOnlyList<IItemMenuProvider> Providers { get; private set; } = [];
        public FeatureManifest Manifest { get; } = new("probe", [Game.PoE1], [], [], [], "Menu probe");
        public void Register(IFeatureContext context) => Providers = context.ItemMenus;
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
