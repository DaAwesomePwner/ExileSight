using ExileSight.Core.Features;
using ExileSight.Core.Input;
using Xunit;

namespace ExileSight.Features.Timers.Tests;

public sealed class BindingTests
{
    [Fact]
    public void DefaultHoldIsPassiveAndToggleYieldsToDisabledUserBinding()
    {
        using var folder = new TestFolder();
        var model = new TimersModel(new TimerStore(folder.Path), new Clock());
        var host = new FeatureHost([new TimersFeature(model)], (_, _) => null, (_, _, _) => { });
        Assert.Empty(Assert.Single(host.Manifests).GameActions);
        var mapped = BindingMapper.Map(host.DefaultBindings.Select(b => b.ToSetting()), new HotkeyConfiguration(), host.Commands);
        var hold = Assert.Single(mapped.Bindings, b => b.Key == 9);
        Assert.False(hold.Block); Assert.True(hold.TrackRelease); Assert.Null(hold.CommandAction);
        var stored = host.DefaultBindings.Single(b => b.Id == "timers.toggle").ToSetting(); stored.Enabled = false;
        var overridden = FeatureDefaultBinding.Apply([stored], host.DefaultBindings);
        var result = BindingMapper.Map(overridden, new HotkeyConfiguration(), host.Commands);
        Assert.Single(result.Bindings); Assert.Contains(result.Rejected, r => r.Contains("Disabled"));
    }
    [Fact]
    public void VisibilityNotificationsFollowEdgesImmediatelyAndIgnoreUnchangedState()
    {
        using var folder = new TestFolder(); var model = new TimersModel(new TimerStore(folder.Path), new Clock());
        var states = new List<TimersVisibility>(); model.VisibilityChanged += states.Add;
        model.Hold(1); Assert.Equal(new TimersVisibility(true, false), Assert.Single(states));
        model.Hold(2); model.Release(1); Assert.Single(states);
        model.Release(2); Assert.Equal(new TimersVisibility(false, false), states[1]);
        model.Toggle(); Assert.Equal(new TimersVisibility(true, true), states[2]);
        model.LoseFocus(); model.LoseFocus(); Assert.Equal(4, states.Count);
    }
    [Fact]
    public void StaleReleaseCannotCloseNewHoldAndFocusLossClearsModes()
    {
        using var folder = new TestFolder(); var model = new TimersModel(new TimerStore(folder.Path), new Clock());
        model.Hold(1); model.Hold(2); model.Release(1); Assert.True(model.Visible);
        model.Toggle(); model.Release(2); Assert.True(model.Visible); Assert.True(model.Interactive);
        model.Toggle(); Assert.False(model.Visible);
        model.Hold(3); model.Toggle(); model.LoseFocus(); Assert.False(model.Visible); Assert.False(model.Interactive);
    }
}
