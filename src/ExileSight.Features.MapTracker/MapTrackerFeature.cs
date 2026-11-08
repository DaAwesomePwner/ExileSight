using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Input;
namespace ExileSight.Features.MapTracker;

[DeclaredActions(ActionKind.SendChatCommand)]
public sealed class MapTrackerFeature(TrackerModel? model) : IFeature
{
    public MapTrackerFeature() : this(null) { }
    public FeatureManifest Manifest { get; } = new("maptracker", [Game.PoE1], [ActionKind.SendChatCommand],
        [], ["poe.ninja"], "Local map history; /kills only on Ctrl+Shift+K. Viewer: Ctrl+Shift+M.", ["prices"]);
    public void Register(IFeatureContext context)
    {
        var tracker = model ?? throw new InvalidOperationException("Compose map tracker storage.");
        if (context.ReadSetting("league") is { ValueKind: System.Text.Json.JsonValueKind.String } saved)
            tracker.SelectLeague(saved.GetString()!);
        tracker.LeagueSelected += value => context.WriteSetting("league", System.Text.Json.JsonSerializer.SerializeToElement(value));
        context.ObserveCopiedItems(tracker.Copied);
        context.SubscribeLog(tracker.Accept, new LogReplay());
        context.RegisterOverlay(tracker);
        context.RegisterCommand("kills", ActionKind.SendChatCommand,
            token => context.Execute(token, new GameAction.SendChatCommand("/kills")));
        context.RegisterCommand("viewer", null, _ => { tracker.Open(); return new(ActionOutcome.Success); });
        context.RegisterDefaultBinding(new("maptracker.kills", 0x4B, KeyModifiers.Control | KeyModifiers.Shift, true, "maptracker:kills"));
        context.RegisterDefaultBinding(new("maptracker.viewer", 0x4D, KeyModifiers.Control | KeyModifiers.Shift, true, "maptracker:viewer"));
    }
}
