using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Input;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class DefaultCollisionTests
{
    [Fact]
    public void UserBindingWinsWhenFeatureDefaultUsesSameKey()
    {
        var host = new FeatureHost([new Module()], (_, _) => null, (_, _, _) => { });
        var persisted = new PersistedBinding
        { Id = "user-copy", Key = 0x14, Block = false, Action = ActionKind.CopyItem };
        var mapped = BindingMapper.Map(FeatureDefaultBinding.Apply([persisted], host.DefaultBindings),
            new HotkeyConfiguration(), host.Commands);
        Assert.Equal("user-copy", Assert.Single(mapped.Bindings).Id);
        Assert.False(mapped.Bindings[0].Block);
        Assert.Contains(mapped.Rejected, r => r.StartsWith("omni.press: Duplicate key", StringComparison.Ordinal));
    }
    private sealed class Module : IFeature
    {
        public FeatureManifest Manifest { get; } = new("omni", [Game.PoE1], [ActionKind.CopyItem], [], [], "test");
        public void Register(IFeatureContext context)
        {
            context.RegisterPressCommand("press", ActionKind.CopyItem, (_, _) => new(ActionOutcome.Success), _ => { });
            context.RegisterDefaultBinding(new("omni.press", 0x14, 0, true, "omni:press"));
        }
    }
}
