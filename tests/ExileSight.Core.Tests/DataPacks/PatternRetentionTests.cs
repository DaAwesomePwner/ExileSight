using System.Runtime.CompilerServices;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

[CollectionDefinition("Stat pattern cache probe", DisableParallelization = true)]
public sealed class PatternCacheProbeCollection;

[Collection("Stat pattern cache probe")]
public sealed class PatternRetentionTests
{
    [Fact]
    public void VersionReplacementClearsCachedPatternsButSourceOnlyPublicationDoesNot()
    {
        var publisher = new PackPublisher();
        publisher.Publish("poe1/stats", Pack("v1"), "fallback");
        var variant = new StatVariant("{0} old", [new()], ["#"], [[]]);
        var original = StatPattern.For(variant);
        publisher.Publish("poe1/stats", Pack("v1"), "cache");
        Assert.Same(original, StatPattern.For(variant));
        publisher.Publish("poe1/stats", Pack("v2"), "cache");
        Assert.NotSame(original, StatPattern.For(variant));
    }

    [Fact]
    public void RefreshAndLateOldVersionMatchesDoNotRetainObsoleteVariantsOrRegexes()
    {
        var publisher = new PackPublisher();
        var old = CacheOldVersion(publisher);
        publisher.Publish("poe1/stats", Pack("v2"), "cache");
        // A caller already holding v1 can finish after the invalidation.
        var late = CacheDetachedVariant();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(old.Variant.IsAlive);
        Assert.False(old.Patterns.IsAlive);
        Assert.False(late.Variant.IsAlive);
        Assert.False(late.Patterns.IsAlive);
        GC.KeepAlive(publisher);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Variant, WeakReference Patterns) CacheOldVersion(PackPublisher publisher)
    {
        publisher.Publish("poe1/stats", Pack("v1"), "fallback");
        return CacheDetachedVariant();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Variant, WeakReference Patterns) CacheDetachedVariant()
    {
        var variant = new StatVariant("{0} old", [new()], ["#"], [[]]);
        return (new(variant), new(StatPattern.For(variant)));
    }

    private static DataPack Pack(string version)
    {
        var content = new PackContent("poe1/stats", Game.PoE1, 3, version, [], [], Translations: []);
        return new(new("poe1/stats", Game.PoE1, 3, version, "test", DateTimeOffset.UnixEpoch,
            new("test", "test", []), "test", PackCodec.Hash(PackCodec.ContentBytes(content))), content);
    }

    [Fact]
    public void EvictionRebuildsIdenticalMatchesInsteadOfRetainingEverySeenPattern()
    {
        var entry = new StatTranslation("old", ["value"], [new("{0} oldest", [new()], ["#"], [[]])]);
        var before = StatText.Match("3 oldest", [entry]);
        var old = StatPattern.For(entry.English[0]);
        for (var i = 0; i <= StatPattern.Capacity; i++)
            _ = StatPattern.For(new("{0} " + i, [new()], ["#"], [[]]));
        Assert.NotSame(old, StatPattern.For(entry.English[0]));
        Assert.Equal(before.Single().Values.Single(), StatText.Match("3 oldest", [entry]).Single().Values.Single());
    }
}
