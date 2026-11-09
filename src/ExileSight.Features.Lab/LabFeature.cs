using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Lab;

[DeclaredActions]
public sealed class LabFeature(LabModel model) : IFeature
{
    public LabFeature() : this(new LabModel()) { }
    public FeatureManifest Manifest { get; } = new("lab", [Game.PoE1], [], [], [],
        "Passive imported daily Labyrinth map and local room tracking.", browserHosts: ["poelab.com", "www.poelab.com"]);
    public void Register(IFeatureContext context)
    {
        context.RegisterOverlay(model);
        context.SubscribeLog(entry => model.Accept(entry.Event));
        context.RegisterCommand("toggle", null, _ => { model.Toggle(); return new(ActionOutcome.Success); });
        context.RegisterDefaultBinding(new("lab.toggle", 0x4C, KeyModifiers.Control | KeyModifiers.Shift, true, "lab:toggle"));
    }
}
