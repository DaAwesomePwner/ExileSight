using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class ModLabelFixtureTests(ITestOutputHelper output)
{
    [Fact]
    public void EveryCopiedModHasAFallbackWithoutPacks() =>
        CheckFixtures(new PackItemInfoData(null, null, null), output, expectMatches: false);

    [Fact]
    public void SyntheticFixtureExercisesMatchedAndUnknownStats()
    {
        // Hand-written synthetic fixture (D-044), not copied game text or a RePoE export.
        var item = ItemTextParser.Parse("""
            Item Class: Body Armours
            Rarity: Rare
            Synthetic Label Guard
            Synthetic Plate
            --------
            Item Level: 80
            --------
            Gain 12 test vitality
            Gain 7 test mystery (crafted)
            """, Game.PoE1).Item!;
        StatTranslation Translation(string id, string template) =>
            new(id, [id], [new(template, [new()], ["#"], [[]])]);
        var data = new PackItemInfoData(null, null, new("synthetic", Game.PoE1, 3, "synthetic", [], [],
            Translations: [Translation("base_maximum_life", "Gain {0} test vitality"),
                Translation("synthetic_unknown_stat", "Gain {0} test mystery")]));
        var labels = item.Mods.Select(line => ModLabels.For(
            Assert.Single(data.MatchLine(line)).Values.Select(v => v.StatId).ToArray(), line.Text)).ToArray();
        Assert.Equal(["+ max life", "Gain test mys…"], labels);
    }

    internal static void CheckFixtures(IItemInfoData data, ITestOutputHelper output, bool expectMatches)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ExileSight.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root.FullName, "tests/fixtures/poe1/items");
        var files = Directory.GetFiles(directory, "*.txt", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(files);
        var tableCount = 0;
        var fallbackCount = 0;
        foreach (var file in files)
        {
            var item = ItemTextParser.Parse(File.ReadAllText(file), Game.PoE1).Item;
            Assert.NotNull(item);
            foreach (var line in item.Mods)
            {
                var match = data.MatchLine(line).FirstOrDefault();
                var ids = match?.Values.Select(v => v.StatId).ToArray() ?? [];
                var label = ModLabels.For(ids, line.Text);
                Assert.InRange(label.Length, 1, 14);
                Assert.DoesNotContain(label, char.IsDigit);
                string? expected = null;
                if (ids.Length == 2) ModLabels.CombinedEntries.TryGetValue((ids[0], ids[1]), out expected);
                if (expected is null && ids.Length > 0) ModLabels.Entries.TryGetValue(ids[0], out expected);
                if (expected is not null)
                {
                    Assert.Equal(expected, label);
                    tableCount++;
                }
                else
                {
                    Assert.Equal(ModLabels.For([], StatText.StripCopySuffixes(line.Text)), label);
                    output.WriteLine($"Fallback: {Path.GetRelativePath(directory, file)} | {line.Text} => {label}");
                    fallbackCount++;
                }
            }
        }
        Assert.Equal(expectMatches, tableCount > 0);
        Assert.True(fallbackCount > 0);
        output.WriteLine($"{files.Length} fixtures: {tableCount} table labels, {fallbackCount} fallbacks.");
    }
}
