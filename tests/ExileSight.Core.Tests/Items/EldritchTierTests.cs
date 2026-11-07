using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Core.Tests.Items;

public sealed class EldritchTierTests
{
    [Fact]
    public void NamedEldritchTiersPreserveImplicitMetadata()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ExileSight.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var item = ItemTextParser.Parse(File.ReadAllText(Path.Combine(root.FullName,
            "tests/fixtures/poe1/items/iteminfo/eldritch.txt")), Game.PoE1).Item!;
        Assert.Equal(new int?[] { 6, 5, 4, 3, 2, 1 }, item.Mods.Select(m => m.Tier));
        Assert.All(item.Mods, m =>
        {
            Assert.True(m.Kind.HasFlag(ModKind.Implicit));
            Assert.False(m.Kind.HasFlag(ModKind.Explicit));
            Assert.True(m.Kind.HasFlag(ModKind.Eater) || m.Kind.HasFlag(ModKind.Exarch));
            Assert.Equal(AffixType.Unknown, m.AffixType);
            Assert.NotNull(m.Header);
        });
    }
}
