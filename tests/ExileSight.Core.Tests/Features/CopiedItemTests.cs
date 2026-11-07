using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;
namespace ExileSight.Core.Tests.Features;

public sealed class CopiedItemTests
{
    [Fact]
    public void ObservationIsImmutableIsolatedAndRunsEvenWithoutMatchingHandler()
    {
        CopiedItem? copy = null; IFeatureContext? captured = null;
        var host = new FeatureHost([new Module(c =>
        {
            captured = c;
            c.ObserveCopiedItems(_ => throw new InvalidOperationException());
            c.ObserveCopiedItems(item => copy = item);
        })], (_, _) => null, (_, _, _) => { });
        string? failure = null; host.ReportWorkFailure = value => failure = value;
        var item = ItemTextParser.Parse("Item Class: Maps\nRarity: Normal\nBeach Map\n--------\nMap Tier: 1", Game.PoE1).Item!;
        item.Mods.Add(new("mod", ModKind.Explicit, AffixType.Unknown, null, null, []));
        Assert.False(host.Route(item, new(Game.PoE1), OmniPressKind.Short));
        Assert.Equal("observer:InvalidOperationException", failure);
        item.Mods.Clear();
        Assert.Equal("Maps", copy!.ItemClass); Assert.Equal(["mod"], copy.Mods);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)copy.Mods).Add("change"));
        Assert.Throws<InvalidOperationException>(() => captured!.ObserveCopiedItems(_ => { }));
    }
    private sealed class Module(Action<IFeatureContext> register) : IFeature
    {
        public FeatureManifest Manifest { get; } = new("observer", [Game.PoE1], [], [], [], "test");
        public void Register(IFeatureContext context) => register(context);
    }
}
