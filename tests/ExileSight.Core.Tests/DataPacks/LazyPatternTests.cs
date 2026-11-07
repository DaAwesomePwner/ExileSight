using System.Reflection;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class LazyPatternTests
{
    [Fact]
    public async Task ValidationIsLazyAndBackgroundPreparationReusesPatterns()
    {
        var pattern = typeof(StatText).Assembly.GetType("ExileSight.Core.DataPacks.StatPattern")!;
        var cache = pattern.GetField("Cache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        object? Cached(StatVariant variant)
        {
            object?[] args = [variant, null];
            return (bool)cache.GetType().GetMethod("TryGetValue")!.Invoke(cache, args)! ? args[1] : null;
        }
        var content = new PackContent("poe1/stats", Game.PoE1, 3, "synthetic", [], [], Translations:
            [new("synthetic", ["test"], [new("{0} Test", [new()], ["#"], [[]])])]);
        var bytes = PackCodec.ContentBytes(content);
        var manifest = new PackManifest("poe1/stats", Game.PoE1, 3, "synthetic", "unknown",
            DateTimeOffset.UnixEpoch, new("synthetic", new string('a', 40), []), "synthetic", PackCodec.Hash(bytes));
        var discarded = PackCodec.Validate(manifest, bytes, "poe1/stats")!;
        Assert.Null(Cached(discarded.Content.Translations![0].English[0]));
        var returned = PackCodec.Validate(manifest, bytes, "poe1/stats")!;
        var variant = returned.Content.Translations![0].English[0];
        Assert.Null(Cached(variant));
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => StatText.PrepareAsync(returned.Content.Translations)));
        var prepared = Cached(variant);
        Assert.NotNull(prepared);
        var compiled = prepared.GetType().GetProperty("Value")!.GetValue(prepared);
        Assert.Single(StatText.Match("3 Test", returned.Content.Translations!));
        Assert.Single(StatText.Match("4 Test", returned.Content.Translations!));
        StatText.Prepare(returned.Content.Translations!);
        Assert.Same(prepared, Cached(variant));
        Assert.Same(compiled, Cached(variant)!.GetType().GetProperty("Value")!.GetValue(Cached(variant)));
    }

    [Theory]
    [InlineData("1.49", 1495)]
    [InlineData("1.5", 1495)]
    [InlineData("-1.49", -1495)]
    [InlineData("-1.5", -1495)]
    public void DecimalMidpointAcceptsBothBinaryDisplayNeighbours(string text, decimal raw)
    {
        var entry = new StatTranslation("synthetic", ["value"],
            [new("{0} Test", [new(raw, raw)], ["#"], [["milliseconds_to_seconds_2dp"]])]);
        Assert.Equal(new StatValue("value", raw, raw), Assert.Single(StatText.Match(text + " Test", [entry])).Values[0]);
        Assert.Empty(StatText.Match("1.48 Test", [entry]));
    }
}
