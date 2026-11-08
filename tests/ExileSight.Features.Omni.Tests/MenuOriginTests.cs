using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Features.Omni;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Omni.Tests;

public sealed class MenuOriginTests
{
    private const string Copy = "Item Class: Rings\nRarity: Normal\nIron Ring";
    [Fact]
    public void ReleaseCapturesPositionBeforeQueuedMenuWork()
    {
        var queue = new Queue<Action>();
        var omni = new OmniFeature();
        var cursor = (X: 100, Y: 200);
        omni.Menu.CursorPosition = () => cursor;
        var host = new FeatureHost([omni], (_, _) => null, (_, _, _) => { },
            (_, _, _) => new(ActionOutcome.Success, CopiedText: Copy), dispatch: queue.Enqueue);
        OmniMenuPresentation? shown = null;
        omni.Menu.Presented += value => shown = value;
        var press = Assert.IsAssignableFrom<IPressCommandHandler>(Assert.Single(host.Commands));
        press.Press(new(), new(1, 10));
        press.Release(new(1, 10, 500));
        cursor = (900, 800);
        queue.Dequeue()();
        Assert.NotNull(shown);
        Assert.Equal(new OmniMenuOrigin(1, 100, 200), shown.Origin);
        Assert.True(omni.Menu.IsCurrent(shown.Origin));
        // The UI dispatcher uses this same check before displaying an already-built result.
        press.Press(new(), new(2, 600));
        Assert.False(omni.Menu.IsCurrent(shown.Origin));
    }
    [Fact]
    public void NewPressDropsQueuedOldMenuEvenBeforeItsOwnRelease()
    {
        var queue = new Queue<Action>();
        var omni = new OmniFeature();
        var host = new FeatureHost([omni], (_, _) => null, (_, _, _) => { },
            (_, _, _) => new(ActionOutcome.Success, CopiedText: Copy), dispatch: queue.Enqueue);
        var opened = 0;
        omni.Menu.Presented += _ => opened++;
        var press = Assert.IsAssignableFrom<IPressCommandHandler>(Assert.Single(host.Commands));
        press.Press(new(), new(1, 10));
        press.Release(new(1, 10, 500));
        press.Press(new(), new(2, 600));
        queue.Dequeue()();
        Assert.Equal(0, opened);
        press.Release(new(2, 600, 900));
        queue.Dequeue()();
        Assert.Equal(1, opened);
    }
    [Fact]
    public void NewPressDuringProviderBuildDropsCompletedOldMenu()
    {
        var menu = new OmniMenu();
        menu.Providers = () => [new Provider(() => menu.BeginPress(2))];
        menu.BeginPress(1);
        var origin = menu.Capture(1);
        var opened = false;
        menu.Presented += _ => opened = true;
        menu.Show(ItemTextParser.Parse(Copy, Game.PoE1).Item!, origin);
        Assert.False(opened);
    }
    [Fact]
    public void CursorFailureDropsReleaseAndReportsOnlyType()
    {
        var omni = new OmniFeature();
        var errors = new List<string>();
        var copies = 0;
        omni.Menu.CursorPosition = () => throw new InvalidOperationException("private path");
        omni.Menu.ReportFailure = errors.Add;
        omni.Menu.Presented += _ => Assert.Fail("Unavailable cursor must drop menu");
        var host = new FeatureHost([omni], (_, _) => null, (_, _, _) => { },
            (_, _, _) => { copies++; return new(ActionOutcome.Success, CopiedText: Copy); }, dispatch: action => action());
        var press = Assert.IsAssignableFrom<IPressCommandHandler>(Assert.Single(host.Commands));
        press.Press(new(), new(1, 10));
        press.Release(new(1, 10, 500));
        press.Release(new(1, 10, 500));
        Assert.Equal(["InvalidOperationException"], errors);
        Assert.Equal(1, copies);
        omni.Menu.ReportFailure = _ => throw new IOException();
        press.Press(new(), new(2, 600));
        press.Release(new(2, 600, 900));
    }
    private sealed class Provider(Action build) : IItemMenuProvider
    {
        public IReadOnlyList<ItemMenuEntry> BuildMenu(ParsedItem item) { build(); return []; }
    }
}
