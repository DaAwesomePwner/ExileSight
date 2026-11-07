using System.Text;
using ExileSight.Core.DataPacks;
using ExileSight.DataGen;
using Xunit;

namespace ExileSight.DataGen.Tests;

public sealed class Phase3Tests
{
    private static byte[] Input(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", name));
    [Fact]
    public void GuideJoinsReferencesAndKeepsConditionsAndDisplayLevels()
    {
        var guide = GuideConverter.Read(Input("guide.json"), Input("world_areas.json"), Input("gems.json"));
        Assert.Equal(["SyntheticTown"], guide.Pages[0].Lines[0].AreaIds);
        Assert.Equal(["3"], guide.Pages[0].Lines[0].LevelHints);
        Assert.Equal(["synthetic_quest"], guide.Pages[0].Lines[1].QuestIds);
        Assert.Equal(["none", "oak"], guide.Pages[1].Condition!.Values);
        Assert.Equal(["yes"], guide.Pages[2].Condition!.Values);
        Assert.Equal("synthetic_quest", Assert.Single(guide.Gems).QuestReward!.QuestId);
        Assert.Equal(3, guide.Gems[0].Levels[0].Requirements["int"]);
    }
    [Theory]
    [InlineData("SyntheticTown", "MissingTown")]
    [InlineData("synthetic_quest1", "missing_quest")]
    [InlineData("bandit", "unsupported")]
    public void GuideRejectsMissingReferencesAndInvalidConditions(string before, string after) =>
        Assert.Throws<InvalidOperationException>(() => GuideConverter.Read(
            Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Input("guide.json")).Replace(before, after)), Input("world_areas.json"), Input("gems.json")));
    [Fact]
    public void MapPatternsRetainNumericAndMultilineSyntaxAndGroupStableIds()
    {
        var entries = MapModsConverter.Read(Input("map-info.txt")).Entries;
        Assert.Equal(2, entries.Length);
        Assert.All(entries, x => Assert.Equal("1", x.Id));
        var entry = entries[0];
        Assert.Equal("1", entry.Id);
        Assert.Equal(2, entry.Patterns.Length);
        Assert.Equal("synthetic # damage|synthetic # speed", entry.Patterns[0]);
        Assert.Throws<InvalidOperationException>(() => MapModsConverter.Read("[broken #]\ntype=player\ntext=broken"u8.ToArray()));
    }
    [Fact]
    public void NewPacksRoundTripAndRequireTheirContent()
    {
        var packs = Phase3Converter.Convert(Input("guide.json"), Input("world_areas.json"), Input("gems.json"), Input("map-info.txt"), new('a', 40), new('b', 40));
        Assert.Equal(4, packs.Length);
        foreach (var pack in packs)
        {
            Assert.NotNull(PackCodec.Validate(pack.Manifest, PackCodec.ContentBytes(pack.Content), pack.Manifest.PackId));
            Assert.All(pack.Manifest.Source.Files.Values, hash => Assert.Equal(64, hash.Length));
        }
        Assert.Contains("HeartofPhos", packs[0].Manifest.Attribution);
    }
}
