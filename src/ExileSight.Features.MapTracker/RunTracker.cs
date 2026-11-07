// Behaviour derived from Exile-UI (MIT), modules/map tracker.ahk and modules/client log.ahk.
using ExileSight.Core.Features;
using ExileSight.Core.Logs;

namespace ExileSight.Features.MapTracker;

/// <summary>Pure event reducer. No raw log text, player names or dialogue bodies survive reduction.</summary>
public sealed class RunTracker
{
    private MapRun? run;
    private string areaId = "", location = "";
    private long seed, lastUptime;
    private DateTime lastTime;
    private bool active, naming;
    private long? baseline;
    private CopiedItem? nextMap;
    public MapRun? Current => run;
    public void Notes(Guid id, string notes) { if (run?.Id == id) run = run with { Notes = notes }; }
    public void Copied(CopiedItem item)
    {
        if (item.ItemClass == "Maps" && item.MapTier is not null) nextMap = item;
    }
    public MapRun? Accept(ClientLogEvent entry, string league)
    {
        if (entry.DroppedBefore > 0 || entry.Event is LogSourceChanged)
        {
            var incomplete = Close(true);
            nextMap = null;
            // The event after a gap can establish a fresh instance, but never extends the old one.
            if (entry.Event is AreaGenerated) Accept(entry with { DroppedBefore = 0 }, league);
            return incomplete;
        }
        if (entry.Event.Envelope is not { } envelope) return null;
        var time = envelope.Timestamp;
        var uptime = envelope.UptimeMilliseconds;
        if (run is not null && (uptime < lastUptime || time < lastTime)) return Close(true);
        if (run is not null && active)
            run = run with { Seconds = run.Seconds + Math.Max(0, (uptime - lastUptime) / 1000d) };
        lastUptime = uptime; lastTime = time;
        MapRun? finished = null;
        switch (entry.Event)
        {
            case AreaGenerated g:
                location = g.AreaId;
                if (IsMap(g.AreaId))
                {
                    if (run is null || areaId != g.AreaId || seed != g.Seed)
                    {
                        finished = Close(false);
                        areaId = g.AreaId; seed = g.Seed; baseline = null;
                        run = new(1, Guid.NewGuid(), league, g.AreaId, g.Level - 67, time, time,
                            0, 1, 0, null, [], nextMap?.Mods.Take(64).Select(m => m[..Math.Min(m.Length, 512)]).ToArray() ?? [], "", false);
                        nextMap = null; naming = true;
                    }
                    else if (!active) run = run with { Portals = run.Portals + 1 };
                    active = true;
                }
                else active = run is not null && !IsSafe(g.AreaId);
                break;
            case AreaEntered e:
                if (IsSafe(e.Name)) active = false;
                else if (run is not null)
                {
                    if (naming) { run = run with { Map = e.Name }; naming = false; }
                    active = true;
                }
                break;
            case PlayerDied when run is not null && active:
                run = run with { Deaths = run.Deaths + 1 }; break;
            case KillCountReported k when run is not null:
                if (baseline is { } b && k.Count >= b) run = run with { Kills = k.Count - b };
                else { baseline = k.Count; run = run with { Kills = null }; }
                break;
            case NpcDialogue npc when run is not null && active && location == areaId:
                if (Mechanic(npc.Npc) is { } mechanic)
                    run = run with { Mechanics = run.Mechanics.Append(mechanic).Distinct().ToArray() };
                break;
        }
        return finished;
    }
    private MapRun? Close(bool incomplete)
    {
        var result = run is null ? null : run with { Ended = lastTime, Incomplete = incomplete };
        run = null; active = false; naming = false; baseline = null;
        return result;
    }
    private static bool IsMap(string id) => id.StartsWith("MapWorlds", StringComparison.Ordinal)
        && !id.Contains("SideArea", StringComparison.Ordinal);
    private static bool IsSafe(string value) => value.Contains("Hideout", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Town", StringComparison.OrdinalIgnoreCase) || value is "Karui Shores" or "Kingsmarch";
    private static string? Mechanic(string npc) => npc switch
    {
        "Strange Voice" => "Delirium", "Sister Cassia" => "Blight", "Oshabi" => "Harvest",
        "Alva, Master Explorer" => "Incursion", "Einhar, Beastmaster" => "Bestiary",
        "Niko, Master of the Depths" => "Delve", "Jun, Veiled Master" => "Betrayal",
        "The Trialmaster" => "Ultimatum", "The Maven" => "Maven",
        "Dannig, Warrior Skald" or "Gwennen, the Gambler" or "Rog" or "Tujen, the Haggler" => "Expedition",
        _ => null
    };
}
