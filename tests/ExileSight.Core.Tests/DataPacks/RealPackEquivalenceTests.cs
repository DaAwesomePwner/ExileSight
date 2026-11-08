using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.DataPacks.Loading;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class RealPackFactAttribute : FactAttribute
{
    public RealPackFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EXILESIGHT_REAL_PACKS")))
            Skip = "Requires the verified shipping packs.";
    }
}

[Collection("Stat pattern cache probe")]
public sealed class RealPackEquivalenceTests
{
    [RealPackFact]
    public async Task CandidateFilterMatchesExhaustiveOracleForEveryFixtureModLine()
    {
        using var transport = new GitHubTransport("equivalence-test");
        var loader = new PackLoader(Environment.GetEnvironmentVariable("EXILESIGHT_REAL_PACKS")!, transport);
        var stats = (await loader.ReadLocalAsync("poe1/stats")).Pack!.Content.Translations!;
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ExileSight.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var lines = Directory.EnumerateFiles(Path.Combine(root.FullName, "tests/fixtures/poe1/items"), "*.txt", SearchOption.AllDirectories)
            .Select(path => ItemTextParser.Parse(File.ReadAllText(path), Game.PoE1).Item)
            .Where(item => item is not null).SelectMany(item => item!.Mods)
            .Select(line => StatText.StripCopySuffixes(line.Text)).Distinct().ToArray();
        Assert.NotEmpty(lines);
        foreach (var line in lines)
            Assert.Equal(JsonSerializer.Serialize(ReferenceStatText.Match(line, stats)), JsonSerializer.Serialize(StatText.Match(line, stats)));
    }

    [Theory]
    [InlineData("{0} Fire {1}", "2 Fire 3")]
    [InlineData("{0} Fire\nCold {1}", "Cold 3")]
    [InlineData("{0}{1}", "23")]
    [InlineData("{0} Foo {0}", "2 Foo 2")]
    [InlineData("固定 {0}", "固定 2")]
    [InlineData("First\r\n{0} Second", "2 Second")]
    public void LiteralFilterPreservesNumbersMultilineBackreferencesAndUnicode(string template, string line)
    {
        var entries = new[] { new StatTranslation("test", ["a", "b"],
            [new(template, [new(), new(3, 3)], ["#", "#"], [[], []])]) };
        Assert.Equal(JsonSerializer.Serialize(ReferenceStatText.Match(line, entries)), JsonSerializer.Serialize(StatText.Match(line, entries)));
    }
}
