using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Input;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class OverlayOnlyBindingTests
{
    private sealed class Feature : IFeature
    {
        public FeatureManifest Manifest => new("test", [Game.PoE1], [], [], [], "Test");
        public void Register(IFeatureContext context)
        {
            context.RegisterCommand("click", null, _ => new(ActionOutcome.Success), CommandKind.OverlayOnly);
            context.RegisterCommand("open", null, _ => new(ActionOutcome.Success));
            context.RegisterPressCommand("hold", null, (_, _) => new(ActionOutcome.Success), _ => { });
            context.RegisterDefaultBinding(new("click", 118, KeyModifiers.None, true, "test:click"));
        }
    }
    private static FeatureHost Host() => new([new Feature()], (_, _) => null, (_, _, _) => { });

    [Theory]
    [InlineData("test:click")]
    [InlineData("TEST:CLICK")]
    [InlineData("Test:Click")]
    public void PersistedOverlayBindingsAreRejectedWithoutChangingStoredValues(string command)
    {
        var binding = new PersistedBinding { Id = "click", Key = 118, FeatureCommand = command };
        var before = JsonSerializer.Serialize(binding);
        var mapped = BindingMapper.Map([binding], new(), Host().Commands);
        Assert.Empty(mapped.Bindings);
        Assert.Contains("Overlay-only command requires a physical overlay click", Assert.Single(mapped.Rejected));
        Assert.Equal(before, JsonSerializer.Serialize(binding));
    }

    [Fact]
    public void FeatureDefaultsAreRejectedWithoutReservingTheirChord()
    {
        var host = Host();
        var source = FeatureDefaultBinding.Apply([], host.DefaultBindings).Append(
            new PersistedBinding { Id = "open", Key = 118, FeatureCommand = "test:open" });
        var mapped = BindingMapper.Map(source, new(), host.Commands);
        Assert.Equal("open", Assert.Single(mapped.Bindings).Id);
        Assert.Contains("Overlay-only", Assert.Single(mapped.Rejected));
        Assert.Single(host.DefaultBindings);
    }

    [Fact]
    public void OrdinaryCommandsAndTokenFreeHoldsStayBindable()
    {
        var host = Host();
        var mapped = BindingMapper.Map([
            new() { Id = "open", Key = 118, FeatureCommand = "test:open" },
            new() { Id = "hold", Key = 119, FeatureCommand = "test:hold" }
        ], new(), host.Commands);
        Assert.Equal(2, mapped.Bindings.Count);
        Assert.Empty(mapped.Rejected);
        Assert.True(mapped.Bindings[1].TrackRelease);
    }
}
