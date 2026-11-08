using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class InfluenceSlotTests
{
    [Theory]
    [InlineData("Two Hand Swords", "Two Hand Sword", "sword", "2h_sword")]
    [InlineData("Two Hand Axes", "Two Hand Axe", "axe", "2h_axe")]
    [InlineData("Two Hand Maces", "Two Hand Mace", "mace", "2h_mace")]
    [InlineData("Rune Daggers", "Rune Dagger", "dagger", "rune_dagger")]
    [InlineData("Warstaves", "Warstaff", "staff", "warstaff")]
    [InlineData("Staves", "Staff", "wrong", "staff")]
    [InlineData("Thrusting One Hand Swords", "Thrusting One Hand Sword", "wrong", "sword")]
    [InlineData("One Hand Swords", "One Hand Sword", "wrong", "sword")]
    [InlineData("One Hand Axes", "One Hand Axe", "wrong", "axe")]
    [InlineData("One Hand Maces", "One Hand Mace", "wrong", "mace")]
    [InlineData("Bows", "Bow", "wrong", "bow")]
    [InlineData("Wands", "Wand", "wrong", "wand")]
    [InlineData("Sceptres", "Sceptre", "wrong", "sceptre")]
    [InlineData("Claws", "Claw", "wrong", "claw")]
    [InlineData("Daggers", "Dagger", "wrong", "dagger")]
    [InlineData("Shields", "Shield", "wrong", "shield")]
    [InlineData("Quivers", "Quiver", "wrong", "quiver")]
    [InlineData("Helmets", "Helmet", "wrong", "helmet")]
    [InlineData("Gloves", "Gloves", "wrong", "gloves")]
    [InlineData("Boots", "Boots", "wrong", "boots")]
    [InlineData("Body Armours", "Body Armour", "wrong", "body_armour")]
    [InlineData("Belts", "Belt", "wrong", "belt")]
    [InlineData("Amulets", "Amulet", "wrong", "amulet")]
    [InlineData("Rings", "Ring", "wrong", "ring")]
    public void ClassSelectsExactlyOneInfluenceSlot(string copiedClass, string packClass, string baseTag, string slot)
    {
        var basis = new BaseEntry("b", "Synthetic Base", packClass, [baseTag, "default"], 1, [], [], []);
        var mod = new ModEntry("correct", "Synthetic", "suffix", "item", "suffix", 84, [], [new("s", 1, 1)],
            [new(slot + "_shaper", 100), new("default", 0)], ["t"]);
        // Identical affix/text/range: eligibility must exclude the wrong slot, even when it comes first.
        var wrong = mod with { Id = "wrong", RequiredLevel = 68, SpawnWeights = [new(baseTag + "_shaper", 100), new("default", 0)] };
        var stats = new StatTranslation("t", ["s"], [new("+{0} Synthetic stat", [new()], ["#"], [[]])]);
        var data = new PackItemInfoData(Pack([basis], [], []), Pack([], [wrong, mod], []), Pack([], [], [stats]));
        var item = Copy(copiedClass, basis.Name, mod.Name, "+1 Synthetic stat");
        Assert.Equal("correct", data.FindMod(item, item.Mods)!.Id);
        Assert.Null(new PackItemInfoData(Pack([basis], [], []), Pack([], [wrong], []), Pack([], [], [stats]))
            .FindMod(item, item.Mods));
        item.Influences.Clear();
        Assert.Null(data.FindMod(item, item.Mods));
    }

    [Fact]
    public async Task FrenzyChargeResolvesTo84WithPinnedPackOrSyntheticData()
    {
        var basis = new BaseEntry("b", "Synthetic Sword", "Two Hand Sword", ["sword", "default"], 1, [], [], []);
        var mod = new ModEntry("synthetic", "Synthetic", "suffix", "item", "suffix", 84, [], [new("s", 1, 1)],
            [new("2h_sword_shaper", 100), new("default", 0)], ["t"]);
        PackContent bases = Pack([basis], [], []), mods = Pack([], [mod], []),
            stats = Pack([], [], [new("t", ["s"], [new("+{0} Synthetic stat", [new()], ["#"], [[]])])]);
        var text = "+1 Synthetic stat";
        var directory = Environment.GetEnvironmentVariable("EXILESIGHT_ITEMINFO_REVIEW_PACKS");
        if (directory is not null)
        {
            PackContent Load(string name) => JsonSerializer.Deserialize(
                File.ReadAllBytes(Path.Combine(directory, "poe1-" + name + ".json")), PackJson.Default.PackContent)!;
            bases = Load("bases"); mods = Load("mods"); stats = Load("stats");
            Assert.Equal("d0bc438357d849bb93ad22f3db0b12938aaf6da1", mods.PackVersion);
            basis = bases.Bases.First(b => b.ItemClass == "Two Hand Sword" && b.Domain == "item");
            mod = mods.Mods.Single(m => m.Id == "MaximumFrenzyChargeUber1");
            text = StatText.Render(stats.Translations!.Single(t => t.Id == mod.TextTemplateIds.Single()), [1])!;
        }
        var data = new PackItemInfoData(bases, mods, stats);
        await data.PrepareAsync();
        var item = Copy("Two Hand Swords", basis.Name, mod.Name, text);
        Assert.Equal(mod.Id, data.FindMod(item, item.Mods)!.Id);
        Assert.Equal(84, Assert.Single(new ItemInfoCalculator(data).Calculate(item).Mods).RequiredLevel);
        // Reuse the same base name with a one-hand class; this mod is never eligible there.
        var oneHand = basis with { ItemClass = "One Hand Sword" };
        var oneHandData = new PackItemInfoData(Pack([oneHand], [], []), mods, stats);
        var wrongItem = Copy("One Hand Swords", basis.Name, mod.Name, text);
        Assert.Null(oneHandData.FindMod(wrongItem, wrongItem.Mods));
    }

    private static ParsedItem Copy(string itemClass, string basis, string name, string text) => ItemTextParser.Parse(
        $"Item Class: {itemClass}\nRarity: Rare\nSynthetic\n{basis}\n--------\n{{ Suffix Modifier \"{name}\" (Tier: 1) }}\n{text}\n--------\nShaper Item", Game.PoE1).Item!;
    private static PackContent Pack(BaseEntry[] bases, ModEntry[] mods, StatTranslation[] stats) =>
        new("synthetic", Game.PoE1, 3, "test", bases, mods, Translations: stats);
}
