using System.Text.Json;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class CalculatorTests
{
    internal static ParsedItem Hybrid() => ItemTextParser.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures/hybrid.txt")), Game.PoE1).Item!;
    [Fact]
    public void MissingPacksPreserveHybridHeadersAndRolls()
    {
        var result = new ItemInfoCalculator().Calculate(Hybrid());
        Assert.Equal(1, result.Prefixes);
        Assert.Equal(1, result.Suffixes);
        Assert.Equal(3, result.Mods.Count);
        Assert.All(result.Mods, m => Assert.Equal(50m, m.RollPosition));
        Assert.All(result.Mods, m => Assert.Null(m.RequiredLevel));
        Assert.All(result.Defences, d => Assert.Null(d.Percentile));
        Assert.Equal(2, result.Mods[0].Source.Tier);
        Assert.True(result.Item.Corrupted);
        Assert.True(result.Mods[2].Source.Kind.HasFlag(ModKind.Fractured));
    }

    [Fact]
    public void EverySlotAndRarityRoutesAndPlainCopyDegrades()
    {
        var cases = JsonSerializer.Deserialize<string[][]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures/slots.json")))!;
        foreach (var row in cases)
        {
            var item = ItemTextParser.Parse($"Item Class: {row[0]}\nRarity: {row[1]}\nSynthetic Item\n--------\nItem Level: 70\n--------\n+20 to maximum Life", Game.PoE1).Item!;
            Assert.True(ItemInfoHandler.Supports(item));
            var result = new ItemInfoCalculator().Calculate(item);
            Assert.Null(result.Prefixes);
            Assert.Null(result.Suffixes);
            Assert.Null(result.UniqueDropTier);
            Assert.All(result.Mods, m => { Assert.Null(m.RollPosition); Assert.Null(m.RequiredLevel); });
        }
    }

    [Fact]
    public void RollSumsRangesAndCanInvert()
    {
        var line = new ModLine("synthetic", ModKind.Explicit, AffixType.Prefix, 1, "Synthetic",
            [new(10, 0, 20), new(30, 20, 40)]);
        Assert.Equal(50, ItemInfoCalculator.Roll(line, false));
        Assert.Equal(50, ItemInfoCalculator.Roll(line, true));
        Assert.Equal(100, ItemInfoCalculator.Roll(line with { Values = [new(5, 5, 5)] }, false));
        Assert.Equal(75, ItemInfoCalculator.Roll(line with { Values = [new(5, 0, 20)] }, true));
    }
}
