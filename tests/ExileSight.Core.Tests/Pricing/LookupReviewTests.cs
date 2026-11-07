using ExileSight.Core.Pricing;
using ExileSight.Core.Items;
using ExileSight.Core.Games;
using ExileSight.Core.Settings;
using System.Text.Json;
using Xunit;

namespace ExileSight.Core.Tests.Pricing;

public sealed class LookupReviewTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(4, 10)]
    [InlineData(5, 50)]
    [InlineData(6, 100)]
    public void MatchesExactHighLinkVariant(int links, int price)
    {
        var overview = PriceParser.Parse(PricingTests.Fixture("links"), "UniqueWeapon");
        var item = new ParsedItem { Game = Game.PoE1, Rarity = ItemRarity.Unique, Name = "Example", BaseType = "Sword" };
        if (links > 0) item.SocketGroups.Add(string.Join("-", Enumerable.Repeat("R", links)));
        Assert.Equal(price, overview.Find(item)!.ChaosValue);
        if (links >= 5)
            Assert.Null((overview with { Items = overview.Items.Where(p => p.Links != links).ToArray() }).Find(item));
    }
    [Theory]
    [InlineData("Example Fossil", "Stackable Currency", "Fossil")]
    [InlineData("Example Oil", "Stackable Currency", "Oil")]
    [InlineData("Example Essence of Test", "Stackable Currency", "Essence")]
    [InlineData("Example Delirium Orb", "Stackable Currency", "DeliriumOrb")]
    [InlineData("Example Resonator", "Stackable Currency", "Resonator")]
    [InlineData("Example Scarab", "Map Fragments", "Scarab")]
    public void RoutesCopiedCurrency(string name, string itemClass, string type)
    {
        var item = new ParsedItem { Game = Game.PoE1, Name = name, ItemClass = itemClass };
        Assert.Equal(type, new PriceOverview([], null).PriceTypeFor(item));
    }
    [Fact]
    public void UsesCoreMetadataAndEmptyHistoryFallback()
    {
        var overview = PriceParser.Parse("""
            {"core":{"primary":"chaos","items":[{"id":"x","name":"Unusual","category":"Fossils"}]},
             "lines":[{"id":"x","primaryValue":1,"sparkLine":{"data":[]},"lowConfidenceSparkLine":{"data":[1,2]}}]}
            """, "Currency");
        var item = new ParsedItem { Game = Game.PoE1, Name = "Unusual", ItemClass = "Stackable Currency" };
        Assert.Equal("Fossil", overview.PriceTypeFor(item));
        Assert.Equal(2, overview.Find(item)!.History.Count);
    }
    [Fact]
    public void NormalizesNullPricesSection()
    {
        var settings = JsonSerializer.Deserialize("""{"prices":null}""", SettingsJsonContext.Default.SettingsDocument)!;
        SettingsValidation.Validate(settings);
        Assert.NotNull(settings.Prices);
        Assert.Equal(60, settings.Prices.RefreshMinutes);
    }
}
