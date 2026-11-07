using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Guide;

[DeclaredActions]
public sealed class GuideFeature(GuideModel model) : IFeature
{
    public GuideFeature() : this(new GuideModel()) { }
    public FeatureManifest Manifest { get; } = new("guide", [Game.PoE1], [], ["poe1/guide"], [],
        "Passive act guide, local navigation, timers and JSON import. Single default profile; no game actions.");
    public void Register(IFeatureContext context)
    {
        void Load()
        {
            if (context.Packs.TryGet<GuideData>("poe1/guide") is { } snapshot) model.Load(snapshot.Data, snapshot.Version);
        }
        context.Packs.Changed += change => { if (change.PackId == "poe1/guide") Load(); };
        Load();
        context.RegisterOverlay(model);
        context.SubscribeLog(model.Accept);
        Register("next", 0x27, () => model.Navigate(1));
        Register("back", 0x25, () => model.Navigate(-1));
        Register("toggle", 0x47, model.Toggle);
        void Register(string command, int key, Action action)
        {
            context.RegisterCommand(command, null, _ => { action(); return new(ActionOutcome.Success); });
            context.RegisterDefaultBinding(new("guide." + command, key, KeyModifiers.Control | KeyModifiers.Shift, true, "guide:" + command));
        }
    }
}
