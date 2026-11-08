using ExileSight.Core.DataPacks;
using ExileSight.DataGen;
using Xunit;

namespace ExileSight.DataGen.Tests;

public sealed class ConversionTests
{
    [Fact]
    public async Task SyntheticExportProducesDeterministicValidatedPacks()
    {
        var bases = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures/base_items.json"));
        var mods = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures/mods.json"));
        var packs = RepoeConverter.Convert(bases, mods, new string('a', 40));
        Assert.All(packs, p => Assert.NotNull(PackCodec.Validate(p.Manifest, PackCodec.ContentBytes(p.Content), p.Manifest.PackId)));
        var hat = Assert.Single(packs[0].Content.Bases);
        Assert.Null(hat.UniqueDropTier);
        Assert.Equal(new NumericRange(4, 9), hat.Defences["armour"]);
        Assert.Equal(new NumericRange(3, 8), hat.Defences["ward"]);
        Assert.False(hat.Defences.ContainsKey("energy_shield"));
        Assert.Equal(7, hat.Requirements["strength"]);
        var entries = Assert.Single(packs[1].Content.TierLadders!).Ranks.ToDictionary(r => r.ModId);
        Assert.Equal(1, entries["Invented1"].Tier);
        Assert.Equal(2, entries["Invented99"].Tier);
        Assert.False(entries.ContainsKey("InventedExcluded"));
        var first = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var second = first + "-second";
        try
        {
            await PackWriter.WriteAsync(first, packs);
            await PackWriter.WriteAsync(second, RepoeConverter.Convert(bases, mods, new string('a', 40)));
            var notice = await File.ReadAllTextAsync(Path.Combine(first, "ATTRIBUTION.txt"));
            Assert.Contains("Game data © Grinding Gear Games", notice);
            Assert.Contains(new string('a', 40), notice);
            Assert.Contains("not affiliated with or endorsed by Grinding Gear Games", notice);
            Assert.Contains("  ATTRIBUTION.txt", await File.ReadAllTextAsync(Path.Combine(first, "SHA256SUMS")));
            foreach (var file in Directory.GetFiles(first))
                Assert.Equal(await File.ReadAllBytesAsync(file), await File.ReadAllBytesAsync(Path.Combine(second, Path.GetFileName(file))));
        }
        finally { Directory.Delete(first, true); Directory.Delete(second, true); }
    }

    [Theory]
    [InlineData("master")]
    [InlineData("abc")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/")]
    public void RejectsUnpinnedSource(string sha) => Assert.Throws<ArgumentException>(() => RepoeConverter.ValidateSha(sha));
}
