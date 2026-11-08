using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Timers;

[DeclaredActions]
public sealed class TimersFeature(TimersModel? suppliedModel) : IFeature
{
    public TimersFeature() : this(null) { }
    private TimersModel Model => suppliedModel ?? throw new InvalidOperationException("Compose the timers model before registration.");
    public FeatureManifest Manifest { get; } = new("timers", [Game.PoE1], [], [], [],
        "Named countdowns and stopwatches with persistent UTC timing and optional local alerts. No game actions.");
    public void Register(IFeatureContext context)
    {
        var model = Model;
        context.RegisterOverlay(model);
        context.RegisterPressCommand("hold", null, (_, press) =>
        {
            model.Hold(press.Id); return new(ActionOutcome.Success);
        }, release => model.Release(release.Id));
        context.RegisterCommand("toggle", null, _ => { model.Toggle(); return new(ActionOutcome.Success); });
        context.RegisterDefaultBinding(new("timers.hold", 0x09, KeyModifiers.None, false, "timers:hold"));
        context.RegisterDefaultBinding(new("timers.toggle", 0x54, KeyModifiers.Control | KeyModifiers.Shift, true, "timers:toggle"));
    }
}
