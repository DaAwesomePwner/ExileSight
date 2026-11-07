using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Core.Tests.Items;

public sealed class MetadataPositionTests
{
    [Theory]
    [InlineData("Corrupted")]
    [InlineData("Mirrored")]
    [InlineData("Unidentified")]
    [InlineData("Unmodifiable")]
    [InlineData("Shaper Item")]
    public void CardRewardDoesNotSetItemMetadata(string reward)
    {
        var result = ItemTextParser.Parse($"Item Class: Divination Cards\nRarity: Divination Card\nThe Reward\n--------\nStack Size: 1/3\n--------\nGold Ring\n{reward}", Game.PoE1);
        Assert.Empty(result.Warnings);
        AssertNoFlags(result.Item!);
        Assert.Equal(new[] { "Gold Ring", reward }, result.Item!.OtherText);
    }

    [Fact]
    public void StandaloneCardRewardIsNotItemMetadata()
    {
        var item = ItemTextParser.Parse("Item Class: Divination Cards\nRarity: Divination Card\nThe Reward\n--------\nStack Size: 1/3\n--------\nCorrupted", Game.PoE1).Item!;
        Assert.False(item.Corrupted);
        Assert.Equal(new[] { "Corrupted" }, item.OtherText);
    }

    [Fact]
    public void FlavourCannotSetMetadata()
    {
        var item = Parse("\"We called it\nCorrupted\nMirrored\nUnidentified\nUnmodifiable\nShaper Item\nand left it behind.\"");
        AssertNoFlags(item);
        Assert.Contains("Corrupted", item.FlavourText);
    }

    [Fact]
    public void MetadataBeforePropertiesIsNotAFlag() =>
        Assert.False(Parse("Corrupted\n--------\nItem Level: 84").Corrupted);

    [Fact]
    public void StandaloneTrailingUnmodifiableSetsFlag()
    {
        var item = Parse("Item Level: 84\n--------\nUnmodifiable\n--------\nNote: ~price 1 divine");
        Assert.True(item.Unmodifiable);
        Assert.Empty(item.OtherText);
        Assert.Empty(item.Mods);
        Assert.Equal(1m, item.Price!.Amount);
    }

    [Fact]
    public void FlagMayPrecedeSupportGemInstructions()
    {
        var result = ItemTextParser.Parse("Item Class: Support Gems\nRarity: Gem\nAdded Fire Damage Support\n--------\nLevel: 1\n--------\nCorrupted\n--------\nThis is a Support Gem. It does not grant a bonus to your character, but to skills in sockets connected to it.", Game.PoE1);
        Assert.True(result.Item!.Corrupted);
        Assert.Single(result.Item.OtherText);
    }

    [Fact]
    public void TrailingFlagsRemainRecognized()
    {
        var item = Parse("Item Level: 84\n--------\nCorrupted\nMirrored\nUnidentified\nUnmodifiable\nShaper Item");
        Assert.True(item.Corrupted);
        Assert.True(item.Mirrored);
        Assert.True(item.Unidentified);
        Assert.True(item.Unmodifiable);
        Assert.Equal(new[] { "Shaper" }, item.Influences);
    }

    private static ParsedItem Parse(string body) => ItemTextParser.Parse(
        "Item Class: Rings\nRarity: Unique\nA Relic\nGold Ring\n--------\n" + body, Game.PoE1).Item!;

    private static void AssertNoFlags(ParsedItem item)
    {
        Assert.False(item.Corrupted);
        Assert.False(item.Mirrored);
        Assert.False(item.Unidentified);
        Assert.False(item.Unmodifiable);
        Assert.Empty(item.Influences);
    }
}
