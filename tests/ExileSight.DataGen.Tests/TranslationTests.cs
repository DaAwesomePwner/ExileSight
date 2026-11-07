using ExileSight.Core.DataPacks;
using Xunit;

namespace ExileSight.DataGen.Tests;

public sealed class TranslationTests
{
    [Fact]
    public async Task IncludesOnlyRelevantTranslationsWithProvenanceAndReferences()
    {
        async Task<byte[]> Read(string name) => await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", name));
        var stats = await Read("stat_translations.json");
        var packs = RepoeConverter.Convert(await Read("base_items.json"), await Read("mods.json"), new string('a', 40), stats);
        var pack = Assert.Single(packs, p => p.Content.PackId == "poe1/stats");
        var translation = Assert.Single(pack.Content.Translations!);
        Assert.Equal("imaginary_glow", Assert.Single(translation.StatIds));
        Assert.Equal(PackCodec.Hash(stats), pack.Manifest.Source.Files["data/stat_translations.json"]);
        Assert.NotNull(PackCodec.Validate(pack.Manifest, PackCodec.ContentBytes(pack.Content), "poe1/stats"));
        var again = RepoeConverter.Convert(await Read("base_items.json"), await Read("mods.json"), new string('a', 40), stats);
        Assert.Equal(PackCodec.ContentBytes(pack.Content), PackCodec.ContentBytes(again[2].Content));
        Assert.Equal("+5 to Imaginary Glow", StatText.Render(translation, [5]));
        Assert.All(packs[1].Content.Mods, m => Assert.Contains(translation.Id, m.TextTemplateIds));
    }
    [Fact]
    public async Task CoverageReportsReachableEndpointLinesAndMissingStats()
    {
        async Task<byte[]> Read(string name) => await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", name));
        var packs = RepoeConverter.Convert(await Read("base_items.json"), await Read("mods.json"),
            new string('a', 40), await Read("stat_translations.json"));
        var report = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await TranslationCoverage.WriteAsync(report, packs);
            using var document = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(report));
            Assert.Equal(6, document.RootElement.GetProperty("totalLines").GetInt32());
            Assert.Equal(0, document.RootElement.GetProperty("unmatchedLines").GetInt32());
            Assert.Equal(0, document.RootElement.GetProperty("missingTranslationStatOccurrences").GetInt32());
        }
        finally { File.Delete(report); }
    }

}
