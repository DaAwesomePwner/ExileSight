using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class HybridDefenceTests
{
    [Fact]
    public void FlatHybridAndTripleIncreaseApplyToTheirDefences()
    {
        var item = ItemTextParser.Parse("""
            Item Class: Body Armours
            Rarity: Rare
            Synthetic
            Synthetic Plate
            --------
            Quality: +20%
            Armour: 196
            Evasion Rating: 210
            Energy Shield: 210
            --------
            +10 to Evasion Rating and Energy Shield
            20% increased Armour, Evasion and Energy Shield
            """, Game.PoE1).Item!;
        var basis = new BaseEntry("b", "Synthetic Plate", "Body Armour", [], 1, [], [],
            new() { ["armour"] = new(100, 180), ["evasion"] = new(100, 180), ["energy_shield"] = new(100, 180) });
        var translations = new[]
        {
            Entry("local_evasion_rating_and_energy_shield", "+{0} to Evasion Rating and Energy Shield"),
            Entry("local_armour_and_evasion_and_energy_shield_+%", "{0}% increased Armour, Evasion and Energy Shield")
        };
        var data = new PackItemInfoData(Pack([basis], []), null, Pack([], translations));
        Assert.All(new ItemInfoCalculator(data).Calculate(item).Defences, d => Assert.Equal(50, d.Percentile));
    }

    [Fact]
    public void UnsupportedPhysicalReductionLocalStatStaysUnknown()
    {
        var item = ItemTextParser.Parse("""
            Item Class: Helmets
            Rarity: Rare
            Synthetic
            Synthetic Hat
            --------
            Armour: 140
            --------
            10% Synthetic modifier
            """, Game.PoE1).Item!;
        var basis = new BaseEntry("b", "Synthetic Hat", "Helmet", [], 1, [], [],
            new() { ["armour"] = new(100, 180) });
        var data = new PackItemInfoData(Pack([basis], []), null, Pack([],
            [Entry("local_physical_damage_reduction_rating_unsupported", "{0}% Synthetic modifier")]));
        Assert.Null(Assert.Single(new ItemInfoCalculator(data).Calculate(item).Defences).Percentile);
    }

    private static StatTranslation Entry(string id, string text) => new(id, [id], [new(text, [new()], ["#"], [[]])]);
    private static PackContent Pack(BaseEntry[] bases, StatTranslation[] translations) =>
        new("synthetic", Game.PoE1, 3, "test", bases, [], Translations: translations);
}
