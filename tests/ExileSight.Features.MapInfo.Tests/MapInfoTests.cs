using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.MapInfo.Tests;

// Pack test snapshot: Lailloken/Exile-UI data/english/map-info.txt (MIT), D-082 pin.
// synthetic-* item fixtures are authored examples, not recordings (D-044).
public sealed class MapInfoTests
{
    private static MapMatcher Matcher() => new(JsonSerializer.Deserialize<MapModsData>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "mapmods.json")))!);
    [Fact]
    public void EveryMapAndLogbookFixtureHasKnownMods()
    {
        foreach (var path in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "fixtures"), "*.txt", SearchOption.AllDirectories))
        {
            var item = ItemTextParser.Parse(File.ReadAllText(path), Game.PoE1).Item!;
            var result = Matcher().Match(item);
            Assert.All(result.Mods, row => Assert.True(row.Id is not null, path + ": " + row.Text));
            if (path.Contains("synthetic-critical"))
            {
                Assert.Equal("012", Assert.Single(result.Mods).Id);
                Assert.Equal(80, result.Quantity); Assert.Equal(40, result.Rarity); Assert.Equal(25, result.PackSize);
                Assert.Contains("300/40", result.Mods[0].Text);
            }
            if (path.Contains("synthetic-areas"))
            {
                Assert.Equal(2, result.Mods.Count);
                Assert.Equal("Desert Ruins (Tujen)", result.Mods[0].Area);
                Assert.Equal("Forest Ruins (Dannig)", result.Mods[1].Area);
            }
        }
    }
    [Fact]
    public void RepeatedModifiersCombineValuesWithinTheSameArea()
    {
        var item = ItemTextParser.Parse("Item Class: Maps\nRarity: Rare\nDoom Road\nDunes Map\n--------\n20% increased Monster Damage\n30% increased Monster Damage", Game.PoE1).Item!;
        var row = Assert.Single(Matcher().Match(item).Mods);
        Assert.Equal("014", row.Id); Assert.Contains("50%", row.Text);
    }
    [Fact]
    public void DifferentAdvancedGroupsCannotBecomeOneMultilineMod()
    {
        var item = ItemTextParser.Parse("Item Class: Maps\nRarity: Rare\nDoom Road\nDunes Map\n--------\n{ Prefix Modifier \"One\" }\nMonsters have 300% increased Critical Strike Chance\n{ Suffix Modifier \"Two\" }\n+40% to Monster Critical Strike Multiplier", Game.PoE1).Item!;
        Assert.All(Matcher().Match(item).Mods, row => Assert.Null(row.Id));
    }
    [Fact]
    public void UnknownIsNeverGuessed()
    {
        var item = ItemTextParser.Parse("Item Class: Maps\nRarity: Normal\nDunes Map\n--------\nMonsters have novel powers", Game.PoE1).Item!;
        Assert.Null(Assert.Single(Matcher().Match(item).Mods).Id);
    }
    [Fact]
    public void RatingsPersistAndCycleAndKeepUnknownIds()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var store = new RatingStore(dir);
            Assert.Equal(0, store.Get("future-id"));
            Assert.Equal(1, store.Cycle("future-id"));
            store = new(dir); Assert.Equal(1, store.Get("future-id"));
            Assert.Equal(2, store.Cycle("future-id")); Assert.Equal(3, store.Cycle("future-id"));
            Assert.Equal(0, store.Cycle("future-id"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
