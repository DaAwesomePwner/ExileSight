// ExileSight-authored feature using passive parsed events; see EventTracker.cs for behaviour provenance.
using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Events;

[DeclaredActions]
public sealed class EventsFeature : IFeature
{
    public EventsModel Model { get; private set; } = new();
    public FeatureManifest Manifest { get; } = new("events", [Game.PoE1], [], ["poe1/events"], [],
        "Passive per-instance map-event notifications with per-event settings. No game actions.");

    public void Register(IFeatureContext context)
    {
        Model = new(save: (id, enabled) => context.WriteSetting("enabled." + id, JsonSerializer.SerializeToElement(enabled)));
        void Refresh()
        {
            var definitions = context.Packs.TryGet<EventsData>("poe1/events")?.Data.Entries;
            Model.Configure(definitions, id => context.ReadSetting("enabled." + id) is { } setting &&
                setting.ValueKind is JsonValueKind.True or JsonValueKind.False ? setting.GetBoolean() : null);
        }
        context.Packs.Changed += change => { if (change.PackId == "poe1/events") Refresh(); };
        Refresh();
        context.SubscribeLog(Model.Accept); // Live only: historical events never become toasts.
        context.RegisterOverlay(Model);
        context.RegisterCommand("settings", null, _ =>
        {
            Model.OpenSettings();
            return new(ActionOutcome.Success);
        });
        context.RegisterDefaultBinding(new("events.settings", 0x45, KeyModifiers.Control | KeyModifiers.Shift, true, "events:settings"));
    }
}
