using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class MatchingEdgeTests
{
    [Theory]
    [InlineData("Utility Flasks", "flask", "utility_flask")]
    [InlineData("Cluster Jewels", "affliction_jewel", "affliction_jewel")]
    [InlineData("Abyss Jewels", "abyss_jewel", "abyss_jewel")]
    public void SpecialCategoriesUseBaseDomainAndSpawnTagsAndFirstMatch(string itemClass, string domain, string tag)
    {
        var item = ItemTextParser.Parse($"Item Class: {itemClass}\nRarity: Rare\nSynthetic\nSynthetic Base\n--------\n{{ Prefix Modifier \"Test\" (Tier: 1) }}\n10(5-15) Glow\n10(5-15) Shine", Game.PoE1).Item!;
        var basis = new BaseEntry("b", "Synthetic Base", itemClass, [tag], 1, [], [], [], Domain: domain);
        var mod = new ModEntry("m", "Test", "prefix", domain, "prefix", 42, [], [new("s", 5, 15)],
            [new(tag, 1)], ["t"]);
        var translation = new StatTranslation("t", ["s"], [new("{0} Glow\n{0} Shine", [new()], ["#"], [[]])]);
        PackContent Pack(BaseEntry[] b, ModEntry[] m, StatTranslation[] t) => new("synthetic", Game.PoE1, 3, "test", b, m, Translations: t);
        var data = new PackItemInfoData(Pack([basis], [], []), Pack([], [mod, mod with { RequiredLevel = 70 }], []),
            Pack([], [], [translation]));
        Assert.Equal(42, data.FindMod(item, item.Mods)!.RequiredLevel);
        Assert.Null(data.FindMod(item, item.Mods.Take(1).ToArray()));
    }

    [Fact]
    public void NormalWithoutAffixesHasZeroCountsAndMapsDoNotRoute()
    {
        var normal = ItemTextParser.Parse("Item Class: Helmets\nRarity: Normal\nSynthetic Hat", Game.PoE1).Item!;
        var result = new ItemInfoCalculator().Calculate(normal);
        Assert.Equal(0, result.Prefixes);
        Assert.Equal(0, result.Suffixes);
        var map = ItemTextParser.Parse("Item Class: Maps\nRarity: Rare\nSynthetic Map", Game.PoE1).Item!;
        Assert.False(ItemInfoHandler.Supports(map));
    }
}
