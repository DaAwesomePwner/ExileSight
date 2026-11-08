using System.Text.Json;
using ExileSight.Features.ItemInfo.Tests.Reference;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class ItemCardPolishTests
{
    [Fact]
    public void RareEnchantHasNoAffixSlot()
    {
        // Hand-written synthetic D-044 fixture.
        var item = ItemCardStateTests.Read("iteminfo/card-enchanted-rare.txt");
        var card = ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(item));
        Assert.Equal("ench", card.Rows[0].Tier);
        Assert.Equal(CardSlotKind.None, card.Rows[0].Slot);
        Assert.Equal(CardSlotKind.Prefix, card.Rows[1].Slot);
    }

    [Fact]
    public void CardSettingsDoNotChangeResultJsonOrValueEquality()
    {
        var result = new ItemInfoCalculator().Calculate(CalculatorTests.Hybrid());
        var settings = new ItemInfoSettings([new("Private profile", [])], ShowCompare: false, ShowItemLevelColumn: false);
        var withSettings = result with { Settings = settings };
        Assert.Equal(result, withSettings);
        Assert.Equal(result.GetHashCode(), withSettings.GetHashCode());
        Assert.NotEqual(result, result with { Prefixes = 2 });
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(withSettings));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(withSettings));
        Assert.False(json.RootElement.TryGetProperty("Settings", out _));
        Assert.Same(settings, withSettings.Settings);
        Assert.False(ItemCardBuilder.Build(withSettings).ShowItemLevelColumn);
    }

    [Fact]
    public void SerializedCalculatorResultStillMatchesFrozenCalculatorWithNondefaultSettings()
    {
        var item = CalculatorTests.Hybrid();
        var settings = new ItemInfoSettings([new("Test", [new(item.Mods[0].Text, Highlight.Desired)])],
            ShowItemLevelColumn: false);
        var expected = new ReferenceItemInfoCalculator().Calculate(item, settings);
        var actual = new ItemInfoCalculator().Calculate(item, settings);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        var card = ItemCardBuilder.Build(actual);
        Assert.NotNull(card.Verdict);
        Assert.False(card.ShowItemLevelColumn);
    }
}
