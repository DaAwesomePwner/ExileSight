using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Features.Omni;
using Xunit;

namespace ExileSight.Features.Omni.Tests;

public sealed class MenuContributionTests
{
    [Fact]
    public void MenuCombinesLinksAndLaterRegisteredContributionsWithoutExecutingThem()
    {
        var omni = new OmniFeature();
        var contribution = new Contribution();
        _ = new FeatureHost([omni, contribution], (_, _) => null, (_, _, _) => { });
        IReadOnlyList<ItemMenuEntry>? entries = null;
        omni.Menu.EntriesOpened += (links, actions) =>
        {
            Assert.NotEmpty(links);
            entries = actions;
        };
        var item = ItemTextParser.Parse("Item Class: Rings\nRarity: Normal\nIron Ring", Game.PoE1).Item!;
        omni.Menu.Show(item);
        Assert.Equal(0, contribution.Calls);
        Assert.Single(entries!).Execute();
        Assert.Equal(1, contribution.Calls);
        Assert.Same(item, contribution.Item);
    }
    private sealed class Contribution : IFeature, IItemMenuProvider
    {
        public FeatureManifest Manifest { get; } = new("test-menu", [Game.PoE1], [], [], [], "test");
        public int Calls { get; private set; }
        public ParsedItem? Item { get; private set; }
        public void Register(IFeatureContext context) => context.RegisterItemMenu(this);
        public IReadOnlyList<ItemMenuEntry> BuildMenu(ParsedItem item) => [new("Set as equipped", () => { Calls++; Item = item; })];
    }
}
