using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class ItemCardValueTests
{
    [Theory]
    [InlineData(1, 100, InfoColor.Best, InfoColor.Best)]
    [InlineData(2, 75, InfoColor.High, InfoColor.High)]
    [InlineData(3, 74, InfoColor.High, InfoColor.Medium)]
    [InlineData(4, 25, InfoColor.Medium, InfoColor.Medium)]
    [InlineData(5, 24, InfoColor.Medium, InfoColor.Low)]
    public void KeepsExactCalculatorToneBoundaries(int tier, int roll, InfoColor tierTone, InfoColor rollTone)
    {
        var item = ItemCardStateTests.Read("iteminfo/card-ring.txt");
        item.Mods[1] = item.Mods[1] with { Tier = tier, Values = [new(roll, 0, 100)] };
        var row = ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(item)).Rows[1];
        Assert.Equal(tierTone, row.TierTone);
        Assert.Equal(rollTone, row.RollTone);
        Assert.Equal(roll, row.RollPosition);
    }

    [Fact]
    public void MatchedHybridCarriesRequiredLevelAndComputedBasePercentile()
    {
        var item = CalculatorTests.Hybrid();
        StatTranslation Translation(string id, string text) => new(id, [id], [new(text, [new()], ["#"], [[]])]);
        PackContent Pack(BaseEntry[] bases, ModEntry[] mods, StatTranslation[] stats) =>
            new("synthetic", Game.PoE1, 3, "synthetic", bases, mods, Translations: stats);
        const string flat = "local_base_physical_damage_reduction_rating";
        const string inc = "local_physical_damage_reduction_rating_+%";
        var basis = new BaseEntry("test", "Synthetic Plate", "Body Armours", ["body_armour"], 1, [], [],
            new() { ["armour"] = new(100, 200) });
        var mod = new ModEntry("test", "Synthetic", "prefix", "item", "prefix", 42, [],
            [new(flat, 5, 15), new(inc, 10, 30)], [new("body_armour", 1)], [flat, inc]);
        var data = new PackItemInfoData(Pack([basis], [], []), Pack([], [mod], []), Pack([], [],
            [Translation(flat, "+{0} to Armour"), Translation(inc, "{0}% increased Armour"),
             Translation("base_fire_damage_resistance_%", "+{0}% to Fire Resistance")]));
        var result = new ItemInfoCalculator(data).Calculate(item);
        var card = ItemCardBuilder.Build(result, data);
        Assert.Equal(42, card.Rows[0].RequiredLevel);
        Assert.Equal(CardLevelKind.Known, card.Rows[0].LevelKind);
        Assert.Equal(CardLevelKind.Unknown, card.Rows[1].LevelKind);
        Assert.Contains(card.Defences, d => d.Name == "Armour" && d.Percentile != null);
        Assert.Equal(result.Defences, card.Defences);
    }

    [Fact]
    public void PairResistanceLabelsUseSpaceAndFullChaosName()
    {
        foreach (var pair in ModLabels.Entries.Where(p => p.Key.Contains("_and_") && p.Key.Contains("resistance")))
        {
            Assert.StartsWith("+ ", pair.Value);
            Assert.InRange(pair.Value.Length, 1, 14);
            if (pair.Key.Contains("chaos")) Assert.Contains("chaos", pair.Value);
        }
        Assert.Equal("+ phys dmg", ModLabels.For(["local_minimum_added_physical_damage"], "unused"));
    }
}
