using ExileSight.Core.DataPacks.Loading;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.ItemInfo.Tests;

[Collection("Real pack probes")]
public sealed class RealPackModLabelTests(ITestOutputHelper output)
{
    [RealPacksFact]
    public async Task TableKeysExistInShippingStatsAndEveryFixtureRowHasALabel()
    {
        using var transport = new GitHubTransport("mod-label-coverage");
        var loader = new PackLoader(Environment.GetEnvironmentVariable("EXILESIGHT_REAL_PACKS")!, transport);
        var loaded = await loader.ReadLocalAsync("poe1/stats");
        Assert.NotNull(loaded.Pack);
        var stats = loaded.Pack.Content;
        var ids = stats.Translations!.SelectMany(t => t.StatIds).ToHashSet(StringComparer.Ordinal);
        Assert.All(ModLabels.Entries.Keys, id => Assert.Contains(id, ids));
        foreach (var pair in ModLabels.CombinedEntries.Keys)
        {
            Assert.Contains(pair.First, ids);
            Assert.Contains(pair.Second, ids);
        }
        ModLabelFixtureTests.CheckFixtures(new PackItemInfoData(null, null, stats), output, expectMatches: true);
    }
}
