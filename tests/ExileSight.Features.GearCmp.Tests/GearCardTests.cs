using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.GearCmp.Tests;

public sealed class GearCardTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private static ParsedItem Fixture(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ExileSight.slnx"))) root = root.Parent;
        return ItemTextParser.Parse(File.ReadAllText(Path.Combine(root!.FullName,
            "tests/fixtures/poe1/items/iteminfo", name)), Game.PoE1).Item!;
    }

    [Fact]
    public void WeaponDpsUsesComponentsAndAbsoluteDirectionalDeltas()
    {
        var model = new GearModel(new GearStore(directory));
        model.Equip(null, GearSlot.MainHand, new("Bows", new()
            { ["Physical DPS"] = 40, ["Elemental DPS"] = 50, ["Chaos DPS"] = 8 }));
        var card = GearCardBuilder.Build(Fixture("card-weapon.txt"), model, _ => [])!;
        var slot = Assert.Single(card.Slots);
        Assert.Equal("main hand", slot.Slot);
        Assert.False(slot.NothingEquipped);
        Assert.Equal(3, slot.Deltas.Count);
        Assert.Contains(new ItemCardDelta("Physical DPS", DeltaDirection.Increase, 20), slot.Deltas);
        Assert.Contains(new ItemCardDelta("Elemental DPS", DeltaDirection.Decrease, 20), slot.Deltas);
        Assert.Contains(new ItemCardDelta("Chaos DPS", DeltaDirection.Unchanged, 0), slot.Deltas);
        Assert.Contains("Default profile", card.Note);
    }

    [Fact]
    public void ArmourUsesCopiedDefencesAndOmitsUnknownStats()
    {
        var model = new GearModel(new GearStore(directory));
        model.Equip(null, GearSlot.Body, new("Body Armours", new() { ["Armour"] = 200, ["Life"] = 40 }));
        var slot = Assert.Single(GearCardBuilder.Build(Fixture("hybrid.txt"), model, _ => [])!.Slots);
        Assert.Contains(new ItemCardDelta("Armour", DeltaDirection.Increase, 16), slot.Deltas);
        Assert.DoesNotContain(slot.Deltas, d => d.Stat == "Life");
    }

    [Fact]
    public void BothRingSlotsShowNothingEquippedWithoutInventedDeltas()
    {
        var model = new GearModel(new GearStore(directory));
        var card = GearCardBuilder.Build(Fixture("card-ring.txt"), model, _ => [])!;
        Assert.Equal(["left ring", "right ring"], card.Slots.Select(s => s.Slot));
        Assert.All(card.Slots, s => { Assert.True(s.NothingEquipped); Assert.Empty(s.Deltas); });
    }

    [Fact]
    public void TwoHanderHeaderNamesBothReplacedHands()
    {
        var model = new GearModel(new GearStore(directory));
        model.Equip(null, GearSlot.MainHand, new("One Hand Swords", new() { ["Physical DPS"] = 10 }));
        model.Equip(null, GearSlot.OffHand, new("Shields", new() { ["Armour"] = 20 }));
        var slot = Assert.Single(GearCardBuilder.Build(Fixture("card-weapon.txt"), model, _ => [])!.Slots);
        Assert.Equal("main + off hand", slot.Slot);
        Assert.Contains(new ItemCardDelta("Armour", DeltaDirection.Decrease, 20), slot.Deltas);
        Assert.Contains(new ItemCardDelta("Physical DPS", DeltaDirection.Increase, 50), slot.Deltas);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
