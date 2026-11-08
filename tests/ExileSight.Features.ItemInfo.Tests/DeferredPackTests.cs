using ExileSight.App.DataPacks;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class DeferredPackTests
{
    [Fact]
    public async Task FailedPreparationCanRetryTheSameVersion()
    {
        var fail = true;
        var translation = new StatTranslation("same", ["value"], [new("{0} Test", [new()], ["#"], [[]])]);
        var startup = new PackStartup((id, _) =>
        {
            var content = new PackContent(id, Game.PoE1, 3, "v1", [], [], Translations:
                id == "poe1/stats" ? fail ? [translation, translation] : [translation] : []);
            var manifest = new PackManifest(id, Game.PoE1, 3, "v1", "test", DateTimeOffset.UnixEpoch,
                new("test", "test", []), "test", PackCodec.Hash(PackCodec.ContentBytes(content)));
            return Task.FromResult<(DataPack?, string)>((new(manifest, content), "fixture"));
        }, (_, _) => Task.CompletedTask);
        await Assert.ThrowsAsync<ArgumentException>(() => startup.LoadAsync("bad", default));
        Assert.Null(startup.Data);
        fail = false;
        await startup.LoadAsync("retry", default);
        Assert.Single(startup.Data!.MatchLine(new("3 Test", ModKind.Explicit, AffixType.Unknown, null, null, [])));
        var prepared = startup.Data;
        await startup.LoadAsync("unchanged", default);
        Assert.Same(prepared, startup.Data);
    }

    [Fact]
    public async Task DeferredAdapterCapturesItsVersionAndDetachesFromMutablePublisherInput()
    {
        var version = "first";
        PackContent? original = null;
        var startup = new PackStartup((id, _) =>
        {
            var content = new PackContent(id, Game.PoE1, 3, version, [], [], Translations:
                id == "poe1/stats" ? [new(version, ["value"], [new("{0} " + version, [new()], ["#"], [[]])])] : []);
            if (id == "poe1/stats" && version == "first") original = content;
            var manifest = new PackManifest(id, Game.PoE1, 3, version, "test", DateTimeOffset.UnixEpoch,
                new("test", "test", []), "test", PackCodec.Hash(PackCodec.ContentBytes(content)));
            return Task.FromResult<(DataPack?, string)>((new(manifest, content), "fixture"));
        }, (_, _) => Task.CompletedTask);
        await startup.LoadAsync("initial", default);
        var old = startup.Data!;
        original!.Translations![0].English[0] = new("mutated", [], [], []);
        version = "second";
        await startup.LoadAsync("replacement", default);
        ModLine Line(string value) => new(value, ModKind.Explicit, AffixType.Unknown, null, null, []);
        Assert.Single(old.MatchLine(Line("3 first")));
        Assert.Empty(old.MatchLine(Line("3 second")));
        Assert.Single(startup.Data!.MatchLine(Line("3 second")));
    }
}
