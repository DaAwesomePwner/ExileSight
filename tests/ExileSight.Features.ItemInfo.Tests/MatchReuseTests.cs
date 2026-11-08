using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class MatchReuseTests
{
    [Fact]
    public void CardReusesMatchesAndAnalysisMetadataDoesNotChangeEqualityOrJson()
    {
        var data = new CountingData();
        var item = CalculatorTests.Hybrid();
        var result = new ItemInfoCalculator(data).Calculate(item);
        var calls = data.Calls;
        _ = ItemCardBuilder.Build(result, data);
        Assert.Equal(item.Mods.Count, calls);
        Assert.Equal(calls, data.Calls);
        var without = new ItemInfoResult(result.Item, result.Mods, result.Prefixes, result.Suffixes,
            result.Defences, result.UniqueDropTier);
        Assert.Equal(without, result);
        Assert.Equal(without.GetHashCode(), result.GetHashCode());
        Assert.Equal(JsonSerializer.Serialize(without), JsonSerializer.Serialize(result));
    }

    private sealed class CountingData : IItemInfoData
    {
        public int Calls;
        public BaseEntry? FindBase(ParsedItem item) => null;
        public ModEntry? FindMod(ParsedItem item, IReadOnlyList<ModLine> group) => null;
        public StatMatch[] MatchLine(ModLine line) { Calls++; return []; }
    }
}
