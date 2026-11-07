using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.GearCmp.Tests;

public sealed class StatsTests
{
    internal static ParsedItem Item(string itemClass) => ItemTextParser.Parse(
        $"Item Class: {itemClass}\nRarity: Normal\nSynthetic Item", Game.PoE1).Item!;
    private static ParsedItem Fixture(string name) => ItemTextParser.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".txt")), Game.PoE1).Item!;
    [Theory]
    [InlineData("Rings", GearSlot.LeftRing, 2)]
    [InlineData("One Hand Swords", GearSlot.MainHand, 2)]
    [InlineData("Shields", GearSlot.OffHand, 1)]
    [InlineData("Quivers", GearSlot.OffHand, 1)]
    [InlineData("Two Hand Axes", GearSlot.MainHand, 1)]
    [InlineData("Helmets", GearSlot.Helmet, 1)]
    [InlineData("Body Armours", GearSlot.Body, 1)]
    [InlineData("Gloves", GearSlot.Gloves, 1)]
    [InlineData("Boots", GearSlot.Boots, 1)]
    [InlineData("Belts", GearSlot.Belt, 1)]
    [InlineData("Amulets", GearSlot.Amulet, 1)]
    public void Slots(string itemClass, GearSlot first, int count)
    {
        var slots = GearSlots.For(Item(itemClass));
        Assert.Equal(count, slots.Length);
        Assert.Equal(first, slots[0]);
    }
    [Fact]
    public void UnknownClassIsNotEquipment() => Assert.Empty(GearSlots.For(Item("Currency")));
    [Fact]
    public void WeaponPropertiesAlreadyContainLocalIncreases()
    {
        var gear = GearStats.Read(Fixture("weapon"), line => line.Text.StartsWith("100%", StringComparison.Ordinal)
            ? [new("local", [new("local_physical_damage_+%", 100, 100)])]
            : [new("life", [new("base_maximum_life", 30, 30)])]);
        Assert.Equal(90, gear.Stats["Physical DPS"]);
        Assert.Equal(82.5m, gear.Stats["Elemental DPS"]);
        Assert.Equal(30, gear.Stats["Life"]);
    }
    [Fact]
    public void ReadsDefencesAndGlobalStats()
    {
        var gear = GearStats.Read(Fixture("armour"), line =>
        {
            var (id, value) = line.Text switch
            {
                "+60 to maximum Life" => ("base_maximum_life", 60),
                "+25 to maximum Mana" => ("base_maximum_mana", 25),
                "+12% to all Elemental Resistances" => ("base_resist_all_elements_%", 12),
                "+15 to all Attributes" => ("base_all_attributes", 15),
                _ => ("base_chaos_damage_resistance_%", 20)
            };
            return [new("synthetic", [new(id, value, value)])];
        });
        Assert.Equal(200, gear.Stats["Armour"]);
        Assert.Equal(120, gear.Stats["Evasion Rating"]);
        Assert.Equal(45, gear.Stats["Energy Shield"]);
        Assert.Equal(10, gear.Stats["Ward"]);
        Assert.Equal(60, gear.Stats["Life"]);
        Assert.Equal(25, gear.Stats["Mana"]);
        foreach (var key in new[] { "Fire resistance", "Cold resistance", "Lightning resistance" }) Assert.Equal(12, gear.Stats[key]);
        foreach (var key in new[] { "Strength", "Dexterity", "Intelligence" }) Assert.Equal(15, gear.Stats[key]);
        Assert.Equal(20, gear.Stats["Chaos resistance"]);
    }
    [Fact]
    public void UnmatchedStatsDoNotBecomeGuessedLosses()
    {
        var unknown = GearStats.Read(Fixture("weapon"), _ => []);
        var delta = GearStats.Delta(unknown, [new("Rings", new() { ["Life"] = 80 })]);
        Assert.False(delta.ContainsKey("Life"));
    }
    [Fact]
    public void DataPackMatcherFeedsExactKnownStatsAndRejectsAmbiguity()
    {
        var translation = new StatTranslation("life", ["base_maximum_life"],
            [new("{0} to maximum Life", [new()], ["+#"], [[]])]);
        var gear = GearStats.Read(Fixture("armour"), line =>
            StatText.Match(StatText.StripCopySuffixes(line.Text), [translation]));
        // Other unmatched lines deliberately make global totals uncertain.
        Assert.Contains("Life", gear.UnknownStats!);
        var weapon = GearStats.Read(Fixture("weapon"), line =>
            [new("a", [new("base_maximum_life", 10, 10)]), new("b", [new("base_maximum_life", 20, 20)])]);
        Assert.False(weapon.Stats.ContainsKey("Life"));
        Assert.Equal(90, weapon.Stats["Physical DPS"]);
    }
    [Fact]
    public void MalformedPropertiesAreUnknownRatherThanZero()
    {
        var item = Item("Helmets");
        item.Properties["Armour"] = new("unknown", []);
        var gear = GearStats.Read(item, _ => []);
        Assert.Empty(GearStats.Delta(gear, [new("Helmets", new() { ["Armour"] = 100 })]));
    }
    [Fact]
    public void DeltaIncludesGainsLossesAndZero()
    {
        var delta = GearStats.Delta(new("Rings", new() { ["Life"] = 60, ["Mana"] = 10 }),
            [new("Rings", new() { ["Life"] = 80, ["Mana"] = 10, ["Strength"] = 20 })]);
        Assert.Equal(-20, delta["Life"]);
        Assert.Equal(0, delta["Mana"]);
        Assert.Equal(-20, delta["Strength"]);
    }
}
