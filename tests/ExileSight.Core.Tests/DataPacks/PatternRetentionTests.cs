using ExileSight.Core.DataPacks;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

[CollectionDefinition("Stat pattern cache probe", DisableParallelization = true)]
public sealed class PatternCacheProbeCollection;

[Collection("Stat pattern cache probe")]
public sealed class PatternRetentionTests
{
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
