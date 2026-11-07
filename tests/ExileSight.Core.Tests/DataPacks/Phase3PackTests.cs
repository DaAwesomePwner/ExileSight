using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.DataPacks.Loading;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class Phase3PackTests
{
    [Theory]
    [InlineData("guide")]
    [InlineData("mapmods")]
    [InlineData("events")]
    [InlineData("leagues")]
    public async Task LoaderReadsNewPackContentAndRejectsMissingContent(string kind)
    {
        var content = new PackContent("poe1/" + kind, Game.PoE1, 3, "synthetic", [], []);
        content = kind switch
        {
            "guide" => content with { Guide = new([new(1, 1, null, [new("Synthetic", [], [], [])])],
                [new("area", "Synthetic", 1, 1, true, true, [])], []) },
            "mapmods" => content with { MapMods = new([new("1", "player", "Synthetic", ["synthetic #"])]) },
            "events" => content with { Events = new([new("Synthetic", "Synthetic", true, "test")]) },
            _ => content with { Leagues = new([new("Synthetic", new(2020, 1, 1), null)]) }
        };
        var bytes = PackCodec.ContentBytes(content);
        var manifest = new PackManifest(content.PackId, Game.PoE1, 3, "synthetic", "unknown", DateTimeOffset.UnixEpoch,
            new("synthetic", "synthetic", []), "Synthetic fixture", PackCodec.Hash(bytes));
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var file = Path.Combine(directory, PackLoader.AssetName(content.PackId) + ".pack.json");
            await File.WriteAllBytesAsync(file, JsonSerializer.SerializeToUtf8Bytes(new CachedPack(manifest, bytes), PackJson.Default.CachedPack));
            using var transport = new GitHubTransport("test");
            var loader = new PackLoader(directory, transport);
            Assert.Equal(PackAvailability.Available, (await loader.ReadLocalAsync(content.PackId)).Availability);
            Assert.Null(PackCodec.Validate(manifest with { SchemaVersion = 999 }, bytes, content.PackId));
            var empty = PackCodec.ContentBytes(content with { Guide = null, MapMods = null, Events = null, Leagues = null });
            Assert.Null(PackCodec.Validate(manifest with { ContentSha256 = PackCodec.Hash(empty) }, empty, content.PackId));
        }
        finally { Directory.Delete(directory, true); }
    }
}
