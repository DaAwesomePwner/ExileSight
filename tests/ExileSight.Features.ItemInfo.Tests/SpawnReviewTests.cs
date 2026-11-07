using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class SpawnReviewTests
{
    [Theory]
    [InlineData("Shaper", "helmet_shaper")]
    [InlineData("Elder", "helmet_elder")]
    [InlineData("Crusader", "helmet_crusader")]
    [InlineData("Redeemer", "helmet_eyrie")]
    [InlineData("Hunter", "helmet_basilisk")]
    [InlineData("Warlord", "helmet_adjudicator")]
    public void InfluenceAddsPackSlotTagsAndFirstMatchingWeightWins(string influence, string tag)
    {
        var item = ItemTextParser.Parse($"Item Class: Helmets\nRarity: Rare\nSynthetic\nSynthetic Hat\n--------\n{{ Prefix Modifier \"Test\" (Tier: 1) }}\n10 Glow\n--------\n{influence} Item", Game.PoE1).Item!;
        var basis = new BaseEntry("b", "Synthetic Hat", "Helmet", ["helmet", "default"], 1, [], [], []);
        var mod = new ModEntry("m", "Test", "prefix", "item", "prefix", 42, [], [new("s", 1, 20)],
            [new(tag, 100), new("default", 0)], ["t"]);
        var translation = new StatTranslation("t", ["s"], [new("{0} Glow", [new()], ["#"], [[]])]);
        PackContent Pack(BaseEntry[] b, ModEntry[] m, StatTranslation[] t) => new("synthetic", Game.PoE1, 3, "test", b, m, Translations: t);
        PackItemInfoData Data(ModEntry m) => new(Pack([basis], [], []), Pack([], [m], []), Pack([], [], [translation]));
        Assert.Equal(42, Data(mod).FindMod(item, item.Mods)!.RequiredLevel);
        Assert.Null(Data(mod with { SpawnWeights = [new(tag, 0), new("helmet", 100)] }).FindMod(item, item.Mods));
        Assert.Equal(42, Data(mod with { SpawnWeights = [new(influence.ToLowerInvariant(), 100), new("default", 0)] })
            .FindMod(item, item.Mods)!.RequiredLevel);
        item.Influences.Clear();
        Assert.Null(Data(mod).FindMod(item, item.Mods));
    }
}
