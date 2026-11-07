using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class PackTests
{
    private static PackContent Pack(BaseEntry[]? bases = null, ModEntry[]? mods = null, StatTranslation[]? stats = null) =>
        new("synthetic", Game.PoE1, 3, "synthetic", bases ?? [], mods ?? [], Translations: stats);
    private static StatTranslation Translation(string id, string text) => new(id, [id], [new(text, [new()], ["#"], [[]])]);
    [Fact]
    public void HybridRequiresAllLinesNameTypeTagsAndDomain()
    {
        var item = CalculatorTests.Hybrid();
        var basis = new BaseEntry("test", "Synthetic Plate", "Body Armours", ["body_armour"], 1, [], [], []);
        var mod = new ModEntry("test", "Synthetic", "prefix", "item", "prefix", 42, [],
            [new("flat", 5, 15), new("inc", 10, 30)], [new("body_armour", 1)], ["flat", "inc"]);
        var translations = new[] { Translation("flat", "+{0} to Armour"), Translation("inc", "{0}% increased Armour") };
        PackItemInfoData Data(ModEntry m) => new(Pack([basis]), Pack(mods: [m]), Pack(stats: translations));
        var result = new ItemInfoCalculator(Data(mod)).Calculate(item);
        Assert.Equal(42, result.Mods[0].RequiredLevel);
        Assert.Equal(42, result.Mods[1].RequiredLevel);
        Assert.Null(result.Mods[2].RequiredLevel);
        foreach (var wrong in new[] { mod with { Name = "wrong" }, mod with { Domain = "flask" },
            mod with { AffixType = "suffix" }, mod with { SpawnWeights = [new("wrong", 1)] },
            mod with { TextTemplateIds = ["flat", "absent"] } })
            Assert.Null(Data(wrong).FindMod(item, item.Mods.Take(2).ToArray()));
        Assert.Null(Data(mod).FindMod(item, item.Mods.Take(1).ToArray()));
    }

    [Theory]
    [InlineData("Armour", "armour", "local_base_physical_damage_reduction_rating", "local_physical_damage_reduction_rating_+%")]
    [InlineData("Evasion Rating", "evasion", "local_base_evasion_rating", "local_evasion_rating_+%")]
    [InlineData("Energy Shield", "energy_shield", "local_energy_shield", "local_energy_shield_+%")]
    [InlineData("Ward", "ward", "local_base_ward", "local_ward_+%")]
    public void ReversesQualityFlatAndIncreased(string property, string key, string flat, string inc)
    {
        var item = ItemTextParser.Parse($"Item Class: Body Armours\nRarity: Rare\nSynthetic\nSynthetic Plate\n--------\nQuality: +20%\n{property}: 216\n--------\n+10 to Test\n20% increased Test", Game.PoE1).Item!;
        var basis = new BaseEntry("test", "Synthetic Plate", "Body Armours", [], 1, [], [],
            new() { [key] = new(100, 180) });
        var data = new PackItemInfoData(Pack([basis]), null, Pack(stats:
            [Translation(flat, "+{0} to Test"), Translation(inc, "{0}% increased Test")]));
        Assert.Equal(50, Assert.Single(new ItemInfoCalculator(data).Calculate(item).Defences).Percentile);
        var equal = new PackItemInfoData(Pack([basis with { Defences = new() { [key] = new(140, 140) } }]),
            null, Pack(stats: [Translation(flat, "+{0} to Test"), Translation(inc, "{0}% increased Test")]));
        Assert.Equal(100, Assert.Single(new ItemInfoCalculator(equal).Calculate(item).Defences).Percentile);
        Assert.Null(Assert.Single(new ItemInfoCalculator().Calculate(item).Defences).Percentile);
        var unsupported = new PackItemInfoData(Pack([basis]), null, Pack(stats:
            [Translation("unsupported_defence_stat", "+{0} to Test"), Translation(inc, "{0}% increased Test")]));
        Assert.Null(Assert.Single(new ItemInfoCalculator(unsupported).Calculate(item).Defences).Percentile);
    }
}
