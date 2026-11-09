using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.MapInfo.Tests;

public sealed class NameAndAreaTests
{
    // Authored synthetic item fixtures under D-044, not client recordings.
    [Theory]
    [InlineData("rare", 16, "rare", true)]
    [InlineData("unique", null, "unique", true)]
    public void MatcherPreservesNameMetadataAtBothReturnSites(string fixture, int? tier, string rarity, bool corrupted)
    {
        var item = ItemTextParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", $"synthetic-name-{fixture}.txt")), Game.PoE1).Item!;
        var data = JsonSerializer.Deserialize<MapModsData>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "mapmods.json")))!;
        var result = new MapMatcher(data).Match(item);
        Assert.Equal(tier, result.Tier);
        Assert.Equal(rarity, result.ItemRarity);
        Assert.Equal(corrupted, result.Corrupted);
        if (fixture == "unique") Assert.Empty(result.Mods); // Early return has no category-bearing rows.
        else
        {
            var row = Assert.Single(result.Mods);
            Assert.Equal("014", row.Id);
            Assert.Equal("monsters", row.Category);
            Assert.Equal("Monsters", MapCardModel.AreaLabel(row));
        }
    }

    [Theory]
    [InlineData(16, "rare", true, "T16 · rare · corrupted")]
    [InlineData(null, "unique", false, "unique")]
    [InlineData(14, null, false, "T14")]
    [InlineData(null, null, true, "corrupted")]
    [InlineData(null, null, false, "")]
    public void MetadataIncludesOnlyKnownParts(int? tier, string? rarity, bool corrupted, string expected)
    {
        var model = new MapCardModel(new("Map", null, null, null, [], tier, rarity, corrupted), new());
        Assert.Equal(expected, model.Metadata);
    }

    [Theory]
    [InlineData("monster", "Monsters")]
    [InlineData("monsters", "Monsters")]
    [InlineData("player", "Players")]
    [InlineData("players", "Players")]
    [InlineData("boss", "Unique boss")]
    [InlineData("area", "Area")]
    [InlineData("expedition", "Expedition")]
    [InlineData("new category", "New Category")]
    [InlineData(null, "")]
    public void CategoriesHaveReadableLabels(string? category, string expected) =>
        Assert.Equal(expected, MapCardModel.AreaLabel(new("id", "Mod", null, Category: category)));

    [Fact]
    public void ExpeditionAreaTakesPrecedenceAndUnknownNeverGuessesArea()
    {
        Assert.Equal("Desert Ruins (Tujen)", MapCardModel.AreaLabel(new("id", "Mod", "Desert Ruins (Tujen)", Category: "expedition")));
        Assert.Equal("Unknown", MapCardModel.AreaLabel(new(null, "Mod", "Desert Ruins (Tujen)", Category: "expedition")));
    }
}
