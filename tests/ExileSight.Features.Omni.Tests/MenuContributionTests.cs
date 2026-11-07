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
        Assert.Equal("test-menu", Assert.Single(entries!).FeatureId);
        Assert.Single(entries!).Execute();
        Assert.Equal(1, contribution.Calls);
        Assert.Same(item, contribution.Item);
    }
    [Fact]
    public void ProviderBuildIsQueuedAndFailuresReportOnlyType()
    {
        var work = new Queue<Action>();
        var omni = new OmniFeature();
        var types = new List<string>();
        omni.Menu.ReportFailure = types.Add;
        var host = new FeatureHost([omni, new FailingContribution()], (_, _) => null, (_, _, _) => { },
            (_, _, _) => new(ExileSight.Platform.Abstractions.Input.ActionOutcome.Success,
                CopiedText: "Item Class: Rings\nRarity: Normal\nIron Ring"), dispatch: work.Enqueue);
        var opened = false;
        omni.Menu.EntriesOpened += (_, _) => opened = true;
        var press = Assert.IsAssignableFrom<ExileSight.Platform.Abstractions.Input.IPressCommandHandler>(Assert.Single(host.Commands));
        press.Press(new(), new(1, 10));
        press.Release(new(1, 10, 500));
        Assert.False(opened);
        Assert.Empty(types);
        Assert.Single(work).Invoke();
        Assert.True(opened);
        Assert.Equal(["InvalidOperationException"], types);
    }
    private sealed class FailingContribution : IFeature, IItemMenuProvider
    {
        public FeatureManifest Manifest { get; } = new("failing", [Game.PoE1], [], [], [], "test");
        public void Register(IFeatureContext context) => context.RegisterItemMenu(this);
        public IReadOnlyList<ItemMenuEntry> BuildMenu(ParsedItem item) => throw new InvalidOperationException("private copied text");
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
