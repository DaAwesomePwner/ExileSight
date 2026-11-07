using System.Globalization;
using System.Text;
using ExileSight.Core.DataPacks;
using ExileSight.DataGen;
using Xunit;

namespace ExileSight.DataGen.Tests;

public sealed class RegressionTests
{
    private static BaseEntry Base(string itemClass = "Paper", string domain = "item") =>
        new("invented", "Imaginary", itemClass, ["paper"], 1, [], [], [], Domain: domain);
    private static ModEntry Mod(string id, int level, string domain = "item", string generation = "prefix") =>
        new(id, id, generation, domain, generation, level, ["synthetic-family"], [new("invented-stat", 1, 2)],
            [new("paper", 1)], ["invented-stat"]);

    [Fact]
    public void LaddersAreSharedAcrossContextsAndEqualLevelsTieStably()
    {
        var mods = new[] { Mod("z", 9), Mod("a", 9), Mod("low", 1) };
        var result = TierDerivation.Derive([Base(), Base("OtherPaper"), Base("ThirdPaper")], mods);
        var ladder = Assert.Single(result.Ladders);
        Assert.Equal(["a", "low", "z"], ladder.Ranks.Select(r => r.ModId));
        Assert.Equal([1, 2, 1], ladder.Ranks.Select(r => r.Tier));
        Assert.Equal(3, result.Contexts.Length);
        Assert.All(result.Contexts, c => Assert.Equal(ladder.Id, Assert.Single(c.LadderIds)));
        var reversed = TierDerivation.Derive([Base("ThirdPaper"), Base()], mods.Reverse().ToArray());
        Assert.Equal(ladder.Id, Assert.Single(reversed.Ladders).Id);
    }

    [Theory]
    [InlineData("item", "implicit")]
    [InlineData("item", "corrupted")]
    [InlineData("item", "enchantment")]
    [InlineData("monster", "prefix")]
    [InlineData("crafted", "suffix")]
    public void UnsupportedModsAreExplicitlyUnranked(string domain, string generation)
    {
        var result = TierDerivation.Derive([Base(), Base(domain: domain)], [Mod("excluded", 3, domain, generation)]);
        Assert.Empty(result.Ladders);
        Assert.All(result.Contexts, c => Assert.Empty(c.LadderIds));
    }

    [Theory]
    [InlineData("prefix")]
    [InlineData("suffix")]
    public void ItemDomainIsRequiredForBasesAndMods(string generation)
    {
        Assert.Empty(TierDerivation.Derive([Base(domain: "monster")], [Mod("one", 2, generation: generation)]).Ladders);
        Assert.Single(TierDerivation.Derive([Base()], [Mod("one", 2, generation: generation)]).Ladders);
    }

    [Fact]
    public async Task OutputUsesFixedLfAndInvariantBytesAcrossCultures()
    {
        Assert.Equal("\n", PackJson.Default.Options.NewLine); // Must hold even on a CRLF platform.
        var bases = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures/base_items.json"));
        var mods = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures/mods.json"));
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            await PackWriter.WriteAsync(path, RepoeConverter.Convert(bases, mods, new string('A', 40)));
            var original = Directory.GetFiles(path).ToDictionary(f => Path.GetFileName(f), File.ReadAllBytes);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            await PackWriter.WriteAsync(path, RepoeConverter.Convert(bases, mods, new string('a', 40)));
            foreach (var file in Directory.GetFiles(path))
            {
                var bytes = await File.ReadAllBytesAsync(file);
                Assert.Equal(original[Path.GetFileName(file)], bytes);
                Assert.DoesNotContain((byte)'\r', bytes);
            }
        }
        finally { CultureInfo.CurrentCulture = before; if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"synthetic\":{\"name\":1}}")]
    [InlineData("{\"synthetic\":{\"name\":\"x\",\"item_class\":\"x\",\"tags\":[],\"drop_level\":99999999999999999999}}")]
    public async Task CliReportsMalformedExportWithoutThrowing(string invalid)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(path, "base_items.json"), invalid);
            await File.WriteAllTextAsync(Path.Combine(path, "mods.json"), "{}");
            Assert.Equal(1, await Program.Main(["poe1", "--repoe-sha", new string('A', 40), "--out", Path.Combine(path, "out"), "--source-dir", path]));
        }
        finally { Directory.Delete(path, true); }
    }

    [Theory]
    [InlineData("main")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaａ")]
    public async Task CliRejectsInvalidCommit(string sha) =>
        Assert.Equal(1, await Program.Main(["poe1", "--repoe-sha", sha, "--out", "unused"]));
}
