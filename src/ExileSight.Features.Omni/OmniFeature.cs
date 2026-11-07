using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Omni;

[DeclaredActions(ActionKind.CopyItem)]
public sealed class OmniFeature : IFeature
{
    public FeatureManifest Manifest { get; } = new("omni", [Game.PoE1], [ActionKind.CopyItem],
        [], [], "Copies one item per press; routes short presses and offers browser links on long presses.",
        ["browser"], OmniLinks.AllowedHosts);
    public OmniMenu Menu { get; } = new();
    public void Register(IFeatureContext context)
    {
        var press = new OmniPress(context, Menu);
        context.RegisterPressCommand("press", ActionKind.CopyItem, press.Start, press.Release);
        context.RegisterDefaultBinding(new("omni.press", 0x14, KeyModifiers.None, true, "omni:press"));
        context.RegisterOverlay(Menu);
    }
}
