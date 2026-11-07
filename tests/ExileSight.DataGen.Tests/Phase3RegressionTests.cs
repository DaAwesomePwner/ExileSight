using System.Text;
using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.DataGen;
using Xunit;

namespace ExileSight.DataGen.Tests;

public sealed class Phase3RegressionTests
{
    private static byte[] Input(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", name));
    private static DataPack[] Convert(Func<string, byte[]> read) => Phase3Converter.Convert(Input("guide.json"),
        Input("world_areas.json"), Input("gems.json"), Input("map-info.txt"), new('a', 40), new('b', 40), read);

    [Fact]
    public void MapModsRevisionChangesVersionButRetainsSourceProvenance()
    {
        var pack = Assert.Single(Convert(Phase3Converter.Authored), p => p.Content.PackId == "poe1/mapmods");
        Assert.Equal(PackCodec.Hash(Encoding.UTF8.GetBytes(new string('b', 40) + ":mapmods-v2")),
            pack.Manifest.PackVersion);
        Assert.Equal(new string('b', 40), pack.Manifest.Source.Sha);
    }

    [Fact]
    public async Task CrLfAuthoredSourcesProduceByteIdenticalOutputs()
    {
        var lf = Convert(Phase3Converter.Authored);
        var crlf = Convert(file => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Phase3Converter.Authored(file)).Replace("\n", "\r\n")));
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await PackWriter.WriteAsync(Path.Combine(directory, "lf"), lf);
            await PackWriter.WriteAsync(Path.Combine(directory, "crlf"), crlf);
            foreach (var path in Directory.GetFiles(Path.Combine(directory, "lf")))
                Assert.Equal(await File.ReadAllBytesAsync(path),
                    await File.ReadAllBytesAsync(Path.Combine(directory, "crlf", Path.GetFileName(path))));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void GuideVersionDoesNotDependOnUnrelatedAuthoredData()
    {
        var original = Convert(Phase3Converter.Authored);
        var changed = Convert(file => file is "events.json" or "leagues.json"
            ? [.. Phase3Converter.Authored(file), (byte)' '] : Phase3Converter.Authored(file));
        Assert.Equal(original[0].Manifest.PackVersion, changed[0].Manifest.PackVersion);
        Assert.Equal(PackCodec.ContentBytes(original[0].Content), PackCodec.ContentBytes(changed[0].Content));
        Assert.NotEqual(original[2].Manifest.PackVersion, changed[2].Manifest.PackVersion);
        Assert.NotEqual(original[3].Manifest.PackVersion, changed[3].Manifest.PackVersion);
    }

    [Fact]
    public void UrlEscapesSpacesAndBracketsWithoutEscapingPathSeparators()
    {
        var url = Phase3Converter.UpstreamUrl(new('a', 40), Phase3Converter.GuidePath);
        Assert.Equal($"https://raw.githubusercontent.com/Lailloken/Exile-UI/{new string('a', 40)}/data/english/%5Bleveltracker%5D%20default%20guide.json", url);
        Assert.Equal(url, new Uri(url).AbsoluteUri);
        Assert.Throws<ArgumentException>(() => Phase3Converter.UpstreamUrl("main", Phase3Converter.GuidePath));
    }

    [Fact]
    public void ActComesFromAreaDataRatherThanChapterPosition()
    {
        var areas = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Input("world_areas.json")).Replace("\"act\":1", "\"act\":7"));
        var guide = GuideConverter.Read(Input("guide.json"), areas, Input("gems.json"));
        Assert.All(guide.Pages, page => Assert.Equal(7, page.Act));
        Assert.Throws<InvalidOperationException>(() => GuideConverter.Read("[[[\"No area reference\"]]]"u8.ToArray(), areas, Input("gems.json")));
    }

    [Fact]
    public void ExistingPackBytesMatchPrePhase3ContentContract()
    {
        var packs = RepoeConverter.Convert(Input("base_items.json"), Input("mods.json"), new('a', 40), Input("stat_translations.json"));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, NewLine = "\n" };
        foreach (var pack in packs)
        {
            var c = pack.Content;
            // Frozen schema-3 content shape from before Phase 3: retain nulls for the original fields.
            var legacy = new { c.PackId, c.Game, c.SchemaVersion, c.PackVersion, c.Bases, c.Mods,
                c.TierLadders, c.TierContexts, c.Translations };
            var before = JsonSerializer.SerializeToUtf8Bytes(legacy, options);
            Assert.Equal(before, PackCodec.ContentBytes(c));
            Assert.Equal(PackCodec.Hash(before), pack.Manifest.ContentSha256);
            Assert.Equal(new string('a', 40), pack.Manifest.PackVersion);
        }
    }
}
