using ExileSight.Core.DataPacks;
using Xunit;

namespace ExileSight.Core.Tests.DataPacks;

public sealed class GuideSharingTests
{
    [Fact]
    public void RepeatedRequirementsAreSharedImmutableAndDetached()
    {
        var first = new Dictionary<string, int> { ["strength"] = 10, ["dexterity"] = 20 };
        var second = new Dictionary<string, int> { ["dexterity"] = 20, ["strength"] = 10 };
        var data = GuideViewBuilder.Build(new([], [],
            [new("a", "A", [new(1, 1, first)], null), new("b", "B", [new(1, 1, second)], null)]));
        var one = data.Gems[0].Levels[0];
        var two = data.Gems[1].Levels[0];
        Assert.Same(one, two);
        first["strength"] = 999;
        Assert.Equal(10, one.Requirements["strength"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)one.Requirements)["strength"] = 999);
    }
}
