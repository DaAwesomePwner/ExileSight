using System.Reflection;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class LazyPatternTests
{
    [Fact]
    public void ValidationDoesNotCompileAndReturnedTranslationCompilesOnlyOnce()
    {
        var counter = typeof(StatText).Assembly.GetType("ExileSight.Core.DataPacks.StatPattern")!
            .GetField("CompilationCount", BindingFlags.NonPublic | BindingFlags.Static)!;
        int Count() => (int)counter.GetValue(null)!;
        var content = new PackContent("poe1/stats", Game.PoE1, 3, "synthetic", [], [], Translations:
            [new("synthetic", ["test"], [new("{0} Test", [new()], ["#"], [[]])])]);
        var bytes = PackCodec.ContentBytes(content);
        var manifest = new PackManifest("poe1/stats", Game.PoE1, 3, "synthetic", "unknown",
            DateTimeOffset.UnixEpoch, new("synthetic", new string('a', 40), []), "synthetic", PackCodec.Hash(bytes));
        var before = Count();
        Assert.NotNull(PackCodec.Validate(manifest, bytes, "poe1/stats"));
        var returned = PackCodec.Validate(manifest, bytes, "poe1/stats")!;
        Assert.Equal(before, Count());
        Assert.Single(StatText.Match("3 Test", returned.Content.Translations!));
        Assert.Single(StatText.Match("4 Test", returned.Content.Translations!));
        StatText.Prepare(returned.Content.Translations!);
        Assert.Equal(before + 1, Count());
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
