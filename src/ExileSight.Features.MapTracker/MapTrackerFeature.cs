using ExileSight.Core.DataPacks;
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
        ["poe1/leagues"], [], "Local map history; /kills only on Ctrl+Shift+K. Viewer: Ctrl+Shift+M.");
    public void Register(IFeatureContext context)
    {
        var tracker = model ?? throw new InvalidOperationException("Compose map tracker storage.");
        void Refresh()
        {
            tracker.Leagues = context.Packs.TryGet<LeaguesData>("poe1/leagues")?.Data.Entries
                .OrderByDescending(e => e.Start).Select(e => e.Name).ToArray() ?? [];
        }
        context.Packs.Changed += _ => Refresh(); Refresh();
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
