// Synthetic cases using IDs verified in generated RePoE pack d0bc438357d849bb93ad22f3db0b12938aaf6da1.
using ExileSight.Core.DataPacks;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.GearCmp.Tests;

public sealed class ReviewStatTests
{
    [Theory]
    [InlineData("fire_and_chaos_damage_resistance_%", "{0}% to Fire and Chaos Resistances", "Fire resistance", "Chaos resistance")]
    [InlineData("cold_and_chaos_damage_resistance_%", "{0}% to Cold and Chaos Resistances", "Cold resistance", "Chaos resistance")]
    [InlineData("lightning_and_chaos_damage_resistance_%", "{0}% to Lightning and Chaos Resistances", "Lightning resistance", "Chaos resistance")]
    [InlineData("fire_and_cold_damage_resistance_%", "{0}% to Fire and Cold Resistances", "Fire resistance", "Cold resistance")]
    [InlineData("fire_and_lightning_damage_resistance_%", "{0}% to Fire and Lightning Resistances", "Fire resistance", "Lightning resistance")]
    [InlineData("cold_and_lightning_damage_resistance_%", "{0}% to Cold and Lightning Resistances", "Cold resistance", "Lightning resistance")]
    [InlineData("additional_strength_and_dexterity", "{0} to Strength and Dexterity", "Strength", "Dexterity")]
    [InlineData("additional_strength_and_intelligence", "{0} to Strength and Intelligence", "Strength", "Intelligence")]
    [InlineData("additional_dexterity_and_intelligence", "{0} to Dexterity and Intelligence", "Dexterity", "Intelligence")]
    [InlineData("additional_all_attributes", "{0} to all Attributes", "Strength", "Dexterity")]
    [InlineData("base_resist_all_elements_%", "{0}% to all Elemental Resistances", "Fire resistance", "Cold resistance")]
    public void HybridMatchesProduceComponentDeltas(string id, string template, string first, string second)
    {
        var item = StatsTests.Item("Rings");
        item.Mods.Add(new(template.Replace("{0}", "+30", StringComparison.Ordinal), ModKind.Explicit,
            AffixType.Unknown, null, null, []));
        var translation = new StatTranslation("synthetic", [id], [new(template, [new()], ["+#"], [[]])]);
        var gear = GearStats.Read(item, line => StatText.Match(line.Text, [translation]));
        var delta = GearStats.Delta(gear, [new("Rings", new() { [first] = 30 })]);
        Assert.Equal(0, delta[first]);
        Assert.Equal(30, delta[second]);
    }
    [Theory]
    [InlineData("maximum_life_+%", "Life")]
    [InlineData("base_maximum_fire_damage_resistance_%", "Fire resistance")]
    [InlineData("strength_+%", "Strength")]
    [InlineData("all_attributes_+%", "Intelligence")]
    [InlineData("all_elemental_resistances_while_moving", "Cold resistance")]
    [InlineData("all_resistances_while_moving", "Chaos resistance")]
    public void UnsupportedTrackedModifiersAreUnknownNotLosses(string id, string label)
    {
        var item = StatsTests.Item("Rings");
        item.Mods.Add(new("Synthetic unsupported modifier", ModKind.Explicit, AffixType.Unknown, null, null, []));
        var gear = GearStats.Read(item, _ => [new("synthetic", [new(id, 30, 30)])]);
        Assert.DoesNotContain(label, GearStats.Delta(gear, [new("Rings", new() { [label] = 30 })]).Keys);
    }
    [Theory]
    [InlineData("life_leech_from_any_damage_permyriad")]
    [InlineData("base_life_regeneration_rate_per_minute")]
    [InlineData("unrecognized_life_leech_modifier")]
    public void UnrelatedStatIdsDoNotHideLife(string id)
    {
        var item = StatsTests.Item("Rings");
        item.Mods.Add(new("Synthetic leech or regeneration", ModKind.Explicit, AffixType.Unknown, null, null, []));
        var gear = GearStats.Read(item, _ => [new("synthetic", [new(id, 30, 30)])]);
        Assert.Equal(-30, GearStats.Delta(gear, [new("Rings", new() { ["Life"] = 30 })])["Life"]);
    }
    [Fact]
    public void ChaosDpsIsSeparateFromElemental()
    {
        var item = StatsTests.Item("Bows");
        item.Properties["Physical Damage"] = new("10-30", []);
        item.Properties["Elemental Damage"] = new("20-40", []);
        item.Properties["Chaos Damage"] = new("30-50 (augmented)", []);
        item.Properties["Attacks per Second"] = new("2.00", []);
        var gear = GearStats.Read(item, _ => []);
        Assert.Equal(40, gear.Stats["Physical DPS"]);
        Assert.Equal(60, gear.Stats["Elemental DPS"]);
        Assert.Equal(80, gear.Stats["Chaos DPS"]);
    }
}
