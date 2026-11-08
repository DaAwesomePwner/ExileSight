using ExileSight.App.DataPacks;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class PackRefreshIdentityTests
{
    [Fact]
    public async Task SourceOnlyChangeReusesAdapterButVersionOrHashRebuilds()
    {
        var version = "1";
        var hash = "first";
        var source = "fallback";
        var startup = new PackStartup((id, _) =>
        {
            var manifest = new PackManifest(id, Game.PoE1, 1, version, "test", DateTimeOffset.UtcNow,
                new("test", "test", []), "test", hash);
            var content = new PackContent(id, Game.PoE1, 1, version, [], []);
            return Task.FromResult<(DataPack?, string)>((new(manifest, content), source));
        }, (_, _) => Task.CompletedTask);
        await startup.LoadAsync("Not checked", default);
        var original = startup.Data;
        Assert.Null(startup.TooltipStatus);
        source = "cache";
        await startup.RefreshAsync(default);
        Assert.Same(original, startup.Data);
        Assert.All(startup.Snapshot().Take(3), row => Assert.Contains("cache", row.Value));
        version = "2";
        await startup.RefreshAsync(default);
        Assert.NotSame(original, startup.Data);
        var versionTwo = startup.Data;
        hash = "second";
        await startup.RefreshAsync(default);
        Assert.NotSame(versionTwo, startup.Data);
    }
}
