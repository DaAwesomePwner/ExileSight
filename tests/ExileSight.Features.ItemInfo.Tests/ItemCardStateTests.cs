using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

// card-*.txt are hand-written synthetic D-044 fixtures, not exported game data.
public sealed class ItemCardStateTests
{
    internal static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ExileSight.slnx"))) dir = dir.Parent;
            return Path.Combine(dir!.FullName, "tests/fixtures/poe1/items");
        }
    }
    internal static ParsedItem Read(string path) => ItemTextParser.Parse(File.ReadAllText(Path.Combine(Root, path)), Game.PoE1).Item!;

    [Fact]
    public void AllAdvancedFixturesPreserveEveryGameLineInOrder()
    {
        var files = Directory.GetFiles(Root, "*.advanced.txt", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var item = Read(file);
            var card = ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(item));
            if (item.Unidentified) { Assert.Empty(card.Rows); continue; }
            Assert.Equal(Enumerable.Range(0, item.Mods.Count), card.Rows.SelectMany(r =>
                Enumerable.Range(r.GameLineStart, r.GameLineCount)));
            Assert.All(card.Rows, r => Assert.InRange(r.Label.Length, 1, 14));
        }
    }

    [Fact]
    public void RareImplicitCraftAndVerdictRespectToggles()
    {
        var item = Read("iteminfo/card-ring.txt");
        var settings = new ItemInfoSettings([new("Test", [new("+30 to maximum Life", Highlight.Desired),
            new("+30% to Fire Resistance", Highlight.Undesired)])]);
        var result = new ItemInfoCalculator().Calculate(item, settings) with { Defences = [new("ES", 94)] };
        var card = ItemCardBuilder.Build(result, settings: settings);
        Assert.Equal("Synthetic Ring", card.Name.BaseType);
        Assert.Equal(80, card.Name.ItemLevel);
        Assert.Equal(1, card.Verdict!.Wanted);
        Assert.Equal(1, card.Verdict.Unwanted);
        Assert.Equal("imp", card.Rows[0].Tier);
        Assert.Equal(CardSlotKind.Implicit, card.Rows[0].Slot);
        Assert.Equal(CardLevelKind.NotApplicable, card.Rows[0].LevelKind);
        Assert.Equal("cr", card.Rows[2].Tier);
        Assert.True(card.Rows[2].Crafted);
        Assert.Equal(CardLevelKind.NotApplicable, card.Rows[2].LevelKind);
        Assert.Equal(InfoColor.Crafted, card.Rows[2].TierTone);
        Assert.Equal(94, card.Defences[0].Percentile);
        var hidden = ItemCardBuilder.Build(result, settings: settings with
            { ShowImplicits = false, ShowDefenceBasePercentile = false, ShowItemLevelColumn = false });
        Assert.Equal(2, hidden.Rows.Count);
        Assert.Equal(1, hidden.Rows[0].GameLineStart);
        Assert.Empty(hidden.Defences);
        Assert.False(hidden.ShowItemLevelColumn);
    }

    [Fact]
    public void UniqueAverageExcludesFixedLinesAndHasNoSlotsOrRequiredLevels()
    {
        var card = ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(Read("rings/kaoms-sign.advanced.txt")));
        Assert.Equal("Kaom's Sign", card.Name.UniqueName);
        Assert.Null(card.Slots);
        Assert.Equal(75, card.Verdict!.AverageRoll);
        Assert.True(card.Rows[2].Fixed);
        Assert.Equal("U", card.Rows[1].Tier);
        Assert.Equal(CardSlotKind.None, card.Rows[1].Slot);
        Assert.Equal(CardSlotKind.None, card.Rows[2].Slot);
        Assert.Equal(CardSlotKind.Implicit, card.Rows[0].Slot);
        Assert.All(card.Rows, r => Assert.Equal(CardLevelKind.NotApplicable, r.LevelKind));
    }

    [Fact]
    public void PlainCopyKeepsHighlightsButNotRangesOrTiers()
    {
        var item = Read("rings/two-affix-magic.plain.txt");
        var settings = new ItemInfoSettings([new("Test", [new(item.Mods[0].Text, Highlight.Desired)])]);
        var card = ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(item, settings), settings: settings);
        Assert.Null(item.BaseType);
        Assert.Equal(item.Name, card.Name.FullName);
        Assert.Equal(item.Name, card.Name.DisplayName);
        Assert.True(card.NeedsAdvancedCopy);
        Assert.NotNull(card.Verdict);
        Assert.Null(card.Slots!.Prefixes.Count);
        Assert.Null(card.Slots.Prefixes.Open);
        Assert.All(card.Rows, r => { Assert.Null(r.RollPosition); Assert.Null(r.RequiredLevel); });
    }

    [Fact]
    public void UnidentifiedSuppressesEverythingExceptNameAndBase()
    {
        var item = ItemTextParser.Parse(File.ReadAllText(Path.Combine(Root, "iteminfo/card-ring.txt")) +
            "\n--------\nUnidentified", Game.PoE1).Item!;
        var card = ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(item) with { Defences = [new("ES", 91)] });
        Assert.True(card.Unidentified);
        Assert.Empty(card.Rows);
        Assert.Null(card.Slots);
        Assert.Null(card.Verdict);
        Assert.Null(card.Compare);
        Assert.Single(card.Defences);
    }

    [Theory]
    [InlineData("Rare", "Rings", 3)]
    [InlineData("Magic", "Wands", 1)]
    [InlineData("Rare", "Jewels", 2)]
    [InlineData("Rare", "Abyss Jewels", 2)]
    [InlineData("Rare", "Unknown Class", null)]
    public void SlotMaximumUsesKnownRarityAndClass(string rarity, string itemClass, int? maximum)
    {
        var text = File.ReadAllText(Path.Combine(Root, "iteminfo/card-ring.txt"))
            .Replace("Rarity: Rare", "Rarity: " + rarity).Replace("Item Class: Rings", "Item Class: " + itemClass);
        var card = ItemCardBuilder.Build(new ItemInfoCalculator().Calculate(ItemTextParser.Parse(text, Game.PoE1).Item!));
        Assert.Equal(maximum, card.Slots!.Prefixes.Maximum);
        Assert.Equal(maximum, card.Slots.Suffixes.Maximum);
    }

    [Fact]
    public void HybridJoinsStatIdsAcrossAllLines()
    {
        StatTranslation Translation(string id, string text) => new(id, [id], [new(text, [new()], ["#"], [[]])]);
        var data = new PackItemInfoData(null, null, new("synthetic", Game.PoE1, 3, "synthetic", [], [],
            Translations: [Translation("local_physical_damage_+%", "{0}% increased Physical Damage"),
                Translation("local_accuracy_rating", "+{0} to Accuracy Rating")]));
        var item = Read("iteminfo/card-weapon.txt");
        var card = ItemCardBuilder.Build(new ItemInfoCalculator(data).Calculate(item), data);
        Assert.Equal("% phys + acc", card.Rows[0].Label);
        Assert.Equal(2, card.Rows[0].GameLineCount);
        Assert.Equal(50, card.Rows[0].RollPosition);
    }
}
