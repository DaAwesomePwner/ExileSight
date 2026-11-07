using ExileSight.App.DataPacks;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class PackReadFailureTests
{
    [Fact]
    public async Task UnexpectedReadFailureRetainsPublishedDataAndItemInfo()
    {
        var fail = false;
        var startup = new PackStartup((id, _) =>
        {
            if (fail) throw new IOException("private path");
            var content = new PackContent(id, Game.PoE1, 3, "v1", [], [],
                Events: id == "poe1/events" ? new([new("id", "name", true, "test")]) : null);
            var manifest = new PackManifest(id, Game.PoE1, 3, "v1", "test", DateTimeOffset.UnixEpoch,
                new("test", "test", []), "test", PackCodec.Hash(PackCodec.ContentBytes(content)));
            return Task.FromResult<(DataPack?, string)>((new(manifest, content), "cache"));
        }, (_, _) => Task.CompletedTask);
        await startup.LoadAsync("initial", default);
        var itemInfo = startup.Data;
        var events = startup.Packs.TryGet<EventsData>("poe1/events")!.Data;
        var changes = new List<PackChange>();
        startup.Packs.Changed += changes.Add;
        fail = true;
        await startup.LoadAsync("retry", default);
        Assert.Same(itemInfo, startup.Data);
        Assert.Same(events, startup.Packs.TryGet<EventsData>("poe1/events")!.Data);
        Assert.Empty(changes);
        Assert.Contains(startup.Snapshot(), row => row.Key == "Last error" && row.Value.EndsWith("IOException", StringComparison.Ordinal));
        Assert.DoesNotContain(startup.Snapshot(), row => row.Value.Contains("private", StringComparison.Ordinal));
        Assert.Contains(startup.Snapshot(), row => row.Key == "Release availability" && row.Value.Contains("404", StringComparison.Ordinal));
    }
}
