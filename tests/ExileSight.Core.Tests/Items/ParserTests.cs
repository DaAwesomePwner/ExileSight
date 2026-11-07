using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Core.Tests.Items;

public sealed class ParserTests
{
    private static ParsedItem Parse(string body) => ItemTextParser.Parse(
        "Item Class: Rings\nRarity: Rare\nDoom Circle\nGold Ring\n--------\n" + body, Game.PoE1).Item!;

    [Fact]
    public void AdvancedHybridRetainsHeaderAndSignedDecimalRanges()
    {
        var item = Parse("{ Prefix Modifier \"Hybrid\" (Tier: 2) — Defences }\n-1.5(-2.5--1.0)% to Critical Strike Chance\n+25(20-30) to maximum Life");
        Assert.Equal(2, item.Mods.Count);
        Assert.Equal(item.Mods[0].Group, item.Mods[1].Group);
        Assert.All(item.Mods, m => { Assert.Equal(2, m.Tier); Assert.Equal("Hybrid", m.AffixName); });
        Assert.Equal(new ModValue(-1.5m, -2.5m, -1m), Assert.Single(item.Mods[0].Values));
    }

    [Theory]
    [InlineData("implicit", ModKind.Implicit)]
    [InlineData("crafted", ModKind.Crafted)]
    [InlineData("fractured", ModKind.Fractured)]
    [InlineData("enchant", ModKind.Enchant)]
    [InlineData("scourge", ModKind.Scourge)]
    [InlineData("veiled", ModKind.Veiled)]
    public void RecognizesMarkers(string marker, ModKind kind) =>
        Assert.True(Assert.Single(Parse($"+20 to maximum Life ({marker})").Mods).Kind.HasFlag(kind));

    [Fact]
    public void ParsesRequirementsAnointmentAndPrice()
    {
        var item = Parse("Requirements:\nLevel: 60\nStr: 100 (unmet)\n--------\nAllocates Charisma (enchant)\n--------\nNote: ~price 3/2 divine");
        Assert.Equal(100, item.Requirements["Str"]);
        Assert.Equal("Charisma", item.Anointment);
        Assert.Equal(1.5m, item.Price!.Amount);
        Assert.Empty(item.ExplicitMods);
    }

    [Fact]
    public void KeepsMultilineUniqueFlavourOutOfMods()
    {
        var result = ItemTextParser.Parse("Item Class: Rings\r\nRarity: Unique\r\nA Relic\r\nGold Ring\r\n--------\r\n+20 to maximum Life\r\n--------\r\n\"We walked together,\r\nuntil the sun went down.\"", Game.PoE1);
        Assert.Single(result.Item!.Mods);
        Assert.Equal(2, result.Item.FlavourText.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("random clipboard")]
    public void InvalidInputReturnsError(string? text) =>
        Assert.Contains(ItemTextParser.Parse(text, Game.PoE1).Warnings, w => w.Severity == WarningSeverity.Error);

    [Fact]
    public void UnsupportedGameIsExplicit() => Assert.Null(ItemTextParser.Parse("anything", Game.PoE2).Item);

    [Fact]
    public void MalformedNumericTextDoesNotThrow()
    {
        var item = Parse("Item Level: 999999999999999999999999999999999999999\n--------\nNote: ~price 1/0 chaos");
        Assert.Null(item.Price);
    }

    [Fact]
    public void PriceOverflowReturnsWarning()
    {
        var result = ItemTextParser.Parse("Item Class: Currency\nRarity: Currency\nChaos Orb\n--------\nNote: ~price 79228162514264337593543950335/0.1 chaos", Game.PoE1);
        Assert.Null(result.Item!.Price);
        Assert.Contains(result.Warnings, w => w.Message.Contains("decimal range", StringComparison.Ordinal));
    }

    [Fact]
    public void NonNumericUniqueModsStayMods()
    {
        var result = ItemTextParser.Parse("Item Class: Rings\nRarity: Unique\nExample\nGold Ring\n--------\n+15% increased Rarity of Items found (implicit)\n--------\nCannot be Frozen", Game.PoE1);
        Assert.Equal("Cannot be Frozen", Assert.Single(result.Item!.ExplicitMods).Text);
        Assert.Empty(result.Item.FlavourText);
    }

    [Fact]
    public void HeaderContextDoesNotLeakAcrossSections()
    {
        var item = Parse("{ Suffix Modifier \"of Ice\" (Tier: 1) — Cold }\n+40(38-42)% to Cold Resistance\n--------\n+20 to maximum Life (crafted)");
        Assert.Equal(AffixType.Suffix, item.Mods[0].AffixType);
        Assert.Null(item.Mods[1].Tier);
    }
}
