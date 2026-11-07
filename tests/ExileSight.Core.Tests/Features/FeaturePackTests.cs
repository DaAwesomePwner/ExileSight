using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class FeaturePackTests
{
    [Fact]
    public void NotificationsAreQueuedOrderedFilteredAndIsolated()
    {
        var source = new FakeReader();
        var feature = new Probe();
        var queue = new Queue<Action>();
        var diagnostics = new List<string>();
        var host = new FeatureHost([feature], (_, _) => null, (_, _, _) => { }, dispatch: queue.Enqueue, packs: source)
        { ReportWorkFailure = diagnostics.Add };
        var observed = new List<string>();
        feature.Packs!.Changed += _ => throw new InvalidOperationException("secret path");
        feature.Packs.Changed += c => observed.Add(c.Version!);
        source.Notify("poe1/guide", "other");
        Assert.Empty(queue);
        Assert.Null(feature.Packs.TryGet<GuideData>("poe1/guide"));
        Assert.Equal(0, source.Reads);
        Assert.Equal("probe: UndeclaredPack", Assert.Single(diagnostics));
        Assert.Null(feature.Packs.TryGet<EventsData>("poe1/events"));
        Assert.Equal(1, source.Reads);
        source.Notify("poe1/events", "v1");
        feature.Context!.DispatchFeature(() => observed.Add("work"));
        source.Notify("poe1/events", "v2");
        Assert.Empty(observed);
        while (queue.TryDequeue(out var action)) action();
        Assert.Equal(new[] { "v1", "work", "v2" }, observed);
        Assert.Equal(new[] { "probe: UndeclaredPack", "probe: InvalidOperationException", "probe: InvalidOperationException" }, diagnostics);
        Assert.IsNotType<PackPublisher>(feature.Packs);
    }

    [Fact]
    public void ParsesOncePerVersionAcrossConcurrentReadsAndSourceChanges()
    {
        var publisher = new PackPublisher();
        var reader = publisher.Reader;
        var content = new PackContent("poe1/events", Game.PoE1, 3, "v1", [], [], Events: new([new("id", "name", true, "test")]));
        var manifest = new PackManifest(content.PackId, Game.PoE1, 3, "v1", "test", DateTimeOffset.UnixEpoch,
            new("test", "test", []), "test", PackCodec.Hash(PackCodec.ContentBytes(content)));
        publisher.Publish(content.PackId, new(manifest, content), "fallback");
        Assert.Null(reader.TryGet<GuideData>(content.PackId));
        var original = reader.TryGet<EventsData>(content.PackId)!;
        Assert.Equal(0, publisher.ParseCount);
        Parallel.For(0, 100, _ => Assert.Same(original.Data, reader.TryGet<EventsData>(content.PackId)!.Data));
        Assert.Equal(1, publisher.ParseCount);
        publisher.Publish(content.PackId, new(manifest, content), "cache");
        Assert.Same(original.Data, reader.TryGet<EventsData>(content.PackId)!.Data);
        Assert.Equal(1, publisher.ParseCount);
        publisher.Publish(content.PackId, new(manifest with { PackVersion = "v2" }, content with { PackVersion = "v2" }), "cache");
        Assert.Equal(1, publisher.ParseCount);
        Assert.NotSame(original.Data, reader.TryGet<EventsData>(content.PackId)!.Data);
        Assert.Equal(2, publisher.ParseCount);
    }
    private sealed class Probe : IFeature
    {
        public FeatureManifest Manifest { get; } = new("probe", [Game.PoE1], [], ["poe1/events"], [], "test");
        public IFeatureContext? Context { get; private set; }
        public IPackReader? Packs => Context?.Packs;
        public void Register(IFeatureContext context) => Context = context;
    }
    private sealed class FakeReader : IPackReader
    {
        public int Reads { get; private set; }
        public PackSnapshot<T>? TryGet<T>(string id) where T : class { Reads++; return null; }
        public event Action<PackChange>? Changed;
        public void Notify(string id, string version) => Changed?.Invoke(new(id, version, "cache"));
    }
}
