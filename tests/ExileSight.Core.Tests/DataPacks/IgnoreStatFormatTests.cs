using System.Text.Json;
using ExileSight.Core.DataPacks;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class IgnoreStatFormatTests
{
    // Hand-written synthetic fixtures; no generated game data.
    private static StatTranslation Entry(int ignoredIndex, bool exactCondition)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ExileSight.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var entries = JsonSerializer.Deserialize<StatTranslation[]>(File.ReadAllText(Path.Combine(root.FullName,
            "tests/fixtures/datagen/ignore-stat-formats.synthetic.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var entry = entries[ignoredIndex];
        if (!exactCondition) entry.English[0].Conditions[ignoredIndex] = new();
        return entry;
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void MatchesOnlyTheDisplayedRollRegardlessOfIgnoredIndex(int ignoredIndex, bool exactCondition)
    {
        var entry = Entry(ignoredIndex, exactCondition);
        StatText.Prepare([entry]);

        var match = Assert.Single(StatText.Match("73% increased Synthetic Damage", [entry]));

        Assert.Equal(entry.Id, match.TranslationId);
        Assert.Equal(new StatValue("synthetic_damage_percent", 73, 73), Assert.Single(match.Values));
        Assert.Equal(new StatValue("synthetic_damage_percent", 60, 80), Assert.Single(
            Assert.Single(StatText.Match("(60–80)% increased Synthetic Damage", [entry])).Values));
        Assert.Equal("73% increased Synthetic Damage", StatText.Render(entry, ignoredIndex == 0 ? [0, 73] : [73, 0]));
    }

    [Fact]
    public void IgnoredOnlyTranslationDoesNotInventARoll()
    {
        var entry = new StatTranslation("synthetic-flag", ["synthetic_hidden_flag"],
            [new("Synthetic flag", [new(1, 1)], ["ignore"], [[]])]);

        Assert.Empty(StatText.Match("Synthetic flag", [entry]));
    }
}
