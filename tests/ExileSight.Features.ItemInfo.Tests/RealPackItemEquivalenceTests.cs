using System.Text.Json;
using ExileSight.Core.DataPacks.Loading;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Features.ItemInfo.Tests.Reference;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.ItemInfo.Tests;

// Run separately from the memory probe: the frozen adapter intentionally retains DTOs.
[Collection("Real pack probes")]
public sealed class RealPackItemEquivalenceTests(ITestOutputHelper output)
{
    [RealPacksFact]
    public async Task EveryFixtureMatchesThePreCompactionItemPath()
    {
        using var transport = new GitHubTransport("item-equivalence");
        var loader = new PackLoader(Environment.GetEnvironmentVariable("EXILESIGHT_REAL_PACKS")!, transport);
        var bases = (await loader.ReadLocalAsync("poe1/bases")).Pack!.Content;
        var mods = (await loader.ReadLocalAsync("poe1/mods")).Pack!.Content;
        var stats = (await loader.ReadLocalAsync("poe1/stats")).Pack!.Content;
        IItemInfoData reference = new ReferenceItemInfoData(bases, mods, stats);
        IItemInfoData compact = new PackItemInfoData(bases, mods, stats);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ExileSight.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var paths = Directory.GetFiles(Path.Combine(root.FullName, "tests/fixtures/poe1/items"), "*.txt", SearchOption.AllDirectories);
        Assert.NotEmpty(paths);
        var checkedItems = 0;
        var matchedGroups = 0;
        void Compare(ParsedItem item, string name)
        {
            checkedItems++;
            foreach (var line in item.Mods)
            {
                var group = line.Group is null ? [line] : item.Mods.Where(m => m.Group == line.Group).ToArray();
                var expected = reference.FindMod(item, group);
                var actual = compact.FindMod(item, group);
                Assert.True(JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(actual), $"FindMod: {name}: {line.Text}");
                if (expected is not null) matchedGroups++;
            }
            // Compare the complete result against the frozen calculator/defence helpers;
            // StatText equivalence has its own frozen-oracle test.
            Assert.True(JsonSerializer.Serialize(new ReferenceItemInfoCalculator(reference).Calculate(item)) ==
                JsonSerializer.Serialize(new ItemInfoCalculator(compact).Calculate(item)), $"ItemInfoCalculator: {name}");
        }
        foreach (var path in paths.Order(StringComparer.Ordinal))
        {
            var item = ItemTextParser.Parse(await File.ReadAllTextAsync(path), Game.PoE1).Item;
            if (item is not null) Compare(item, Path.GetRelativePath(root.FullName, path));
        }
        Compare(FirstUseFixture.Parse(), "synthetic Fecund Glorious Plate");
        Assert.True(checkedItems > 1);
        Assert.True(matchedGroups > 0, "Equivalence must include successful real-pack FindMod results.");
        output.WriteLine($"Compared {paths.Length} fixture files, {checkedItems} parsed items, {matchedGroups} matched groups.");
    }
}
