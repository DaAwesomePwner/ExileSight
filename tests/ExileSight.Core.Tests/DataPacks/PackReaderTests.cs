using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.DataPacks.Loading;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class PackReaderTests
{
    [Fact]
    public void PublicationIsDetachedVersionedAndIsolatesSubscribers()
    {
        var publisher = new PackPublisher();
        var reader = publisher.Reader;
        var changes = new List<PackChange>();
        reader.Changed += _ => throw new InvalidOperationException();
        reader.Changed += changes.Add;
        var pack = MakePack("v1");
        publisher.Publish("poe1/events", pack, "fallback");
        var first = reader.TryGet<EventsData>("poe1/events")!;
        pack.Content.Events!.Entries[0] = new("mutated", "mutated", false, "test");
        first.CreateCopy().Events!.Entries[0] = new("mutated", "mutated", false, "test");
        Assert.Throws<NotSupportedException>(() => ((IList<EventDefinition>)first.Data.Entries)[0] = new("x", "x", false, "x"));
        Assert.Equal("event", first.Data.Entries[0].Id);
        publisher.Publish("poe1/events", MakePack("v1"), "cache");
        Assert.Single(changes);
        publisher.Publish("poe1/events", MakePack("v2"), "cache");
        Assert.Equal("v1", first.Version);
        Assert.Equal("v2", reader.TryGet<EventsData>("poe1/events")!.Version);
        Assert.Equal("cache", reader.TryGet<EventsData>("poe1/events")!.Source);
        Assert.Equal(2, changes.Count);
        Assert.Null(reader.TryGet<GuideData>("poe1/events"));
        publisher.Publish("poe1/events", null, "none");
        Assert.False(changes[^1].Available);
        Assert.Null(reader.TryGet<EventsData>("poe1/events"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrCorruptLocalPackIsUnavailable(bool corrupt)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            if (corrupt) await File.WriteAllTextAsync(Path.Combine(directory, "poe1-events.pack.json"), "{broken");
            using var transport = new GitHubTransport("test");
            var loader = new PackLoader(directory, transport);
            var publisher = new PackPublisher();
        var reader = publisher.Reader;
            publisher.Publish("poe1/events", (await loader.ReadLocalAsync("poe1/events")).Pack, "none");
            Assert.Null(reader.TryGet<EventsData>("poe1/events"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void HostPassesReaderToFeatureAndDefaultsToUnavailable()
    {
        var feature = new ProbeFeature();
        var reader = new FakeReader();
        _ = new FeatureHost([feature], (_, _) => null, (_, _, _) => { }, packs: reader, dispatch: action => action());
        Assert.NotSame(reader, feature.Context!.Packs);
        var notifications = 0;
        feature.Context.Packs.Changed += _ => notifications++;
        reader.Notify();
        Assert.Equal(1, notifications);
        _ = new FeatureHost([feature], (_, _) => null, (_, _, _) => { });
        Assert.Null(feature.Context.Packs.TryGet<EventsData>("poe1/events"));
    }

    [Fact]
    public void AllPhaseThreeModelsAreSelectedByPackIdentity()
    {
        var publisher = new PackPublisher();
        var reader = publisher.Reader;
        var template = MakePack("v1");
        void Publish(string id, PackContent content) => publisher.Publish(id,
            new(template.Manifest with { PackId = id }, content with { PackId = id }), "fallback");
        Publish("poe1/guide", template.Content with { Guide = new([], [], []) });
        Publish("poe1/mapmods", template.Content with { MapMods = new([new("mod", "map", "text", [])]) });
        Publish("poe1/leagues", template.Content with { Leagues = new([new("league", new(2026, 1, 1), null)]) });
        Assert.Empty(reader.TryGet<GuideData>("poe1/guide")!.Data.Pages);
        Assert.Equal("mod", reader.TryGet<MapModsData>("poe1/mapmods")!.Data.Entries[0].Id);
        Assert.Equal("league", reader.TryGet<LeaguesData>("poe1/leagues")!.Data.Entries[0].Name);
        Assert.Equal("poe1/guide", reader.TryGet<GuideData>("poe1/guide")!.CreateCopy().PackId);
    }

    [Fact]
    public async Task RefreshPublishesNewVersionAndRetainsOldSnapshot()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var pack = MakePack("v1");
        using var handler = new RefreshHandler(() => pack);
        using var transport = new GitHubTransport("test", () => handler);
        var clock = new RefreshClock();
        var loader = new PackLoader(path, transport, timeProvider: clock);
        var publisher = new PackPublisher();
        var reader = publisher.Reader;
        var changes = new List<PackChange>();
        reader.Changed += changes.Add;
        try
        {
            publisher.Publish("poe1/events", (await loader.RefreshAsync("poe1/events", "release")).Pack, "cache");
            var original = reader.TryGet<EventsData>("poe1/events")!;
            pack = MakePack("v2");
            clock.Now += TimeSpan.FromHours(1);
            publisher.Publish("poe1/events", (await loader.RefreshAsync("poe1/events", "release")).Pack, "cache");
            Assert.Equal(new[] { "v1", "v2" }, changes.Select(c => c.Version));
            Assert.Equal("v1", original.Version);
            Assert.Equal("v2", reader.TryGet<EventsData>("poe1/events")!.Version);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    private sealed class RefreshClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class RefreshHandler(Func<DataPack> pack) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var current = pack();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(request.RequestUri!.AbsolutePath.EndsWith(".manifest.json", StringComparison.Ordinal)
                    ? JsonSerializer.SerializeToUtf8Bytes(current.Manifest, PackJson.Default.PackManifest)
                    : PackCodec.ContentBytes(current.Content))
            });
        }
    }

    private static DataPack MakePack(string version)
    {
        var content = new PackContent("poe1/events", Game.PoE1, 3, version, [], [],
            Events: new([new("event", "Event", true, "test")]));
        return new(new("poe1/events", Game.PoE1, 3, version, "unknown", DateTimeOffset.UnixEpoch,
            new("synthetic", "synthetic", []), "Synthetic fixture", PackCodec.Hash(PackCodec.ContentBytes(content))), content);
    }
    private sealed class ProbeFeature : IFeature
    {
        public FeatureManifest Manifest { get; } = new("probe", [Game.PoE1], [], ["poe1/events"], [], "Test");
        public IFeatureContext? Context { get; private set; }
        public void Register(IFeatureContext context) => Context = context;
    }
    private sealed class FakeReader : IPackReader
    {
        public PackSnapshot<T>? TryGet<T>(string packId) where T : class => null;
        public event Action<PackChange>? Changed;
        public void Notify() => Changed?.Invoke(new("poe1/events", "v2", "cache"));
    }
}
