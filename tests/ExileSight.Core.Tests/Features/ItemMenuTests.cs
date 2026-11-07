using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class ItemMenuTests
{
    [Fact]
    public void RegistrationIsLiveUntilClosedAndProviderFailuresAreIsolated()
    {
        var feature = new Module();
        _ = new FeatureHost([feature], (_, _) => null, (_, _, _) => { });
        Assert.Equal(2, feature.Context!.ItemMenus.Count);
        Assert.Throws<InvalidOperationException>(() => feature.Context.RegisterItemMenu(new Provider(false)));
        var errors = 0;
        var item = ItemTextParser.Parse("Item Class: Rings\nRarity: Normal\nIron Ring", Game.PoE1).Item!;
        var entry = Assert.Single(ItemMenuEntries.Build(item, feature.Context.ItemMenus, _ => errors++));
        Assert.Equal(1, errors);
        Assert.Equal("Local action", entry.Label);
        entry.Execute();
        Assert.Equal(1, feature.Good.Executions);
    }
    private sealed class Module : IFeature
    {
        public FeatureManifest Manifest { get; } = new("menu-test", [Game.PoE1], [], [], [], "test");
        public IFeatureContext? Context { get; private set; }
        public Provider Good { get; } = new(false);
        public void Register(IFeatureContext context)
        {
            Context = context;
            context.RegisterItemMenu(new Provider(true));
            context.RegisterItemMenu(Good);
        }
    }
    private sealed class Provider(bool fail) : IItemMenuProvider
    {
        public int Executions { get; private set; }
        public IReadOnlyList<ItemMenuEntry> BuildMenu(ParsedItem item) => fail
            ? throw new InvalidOperationException() : [new("Local action", () => Executions++)];
    }
}
