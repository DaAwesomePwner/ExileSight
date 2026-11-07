// Behaviour derived from Exile-UI (MIT), modules/map tracker.ahk and modules/client log.ahk.
using ExileSight.Core.Features;
using ExileSight.Core.Logs;

namespace ExileSight.Features.MapTracker;

/// <summary>Pure event reducer. No raw log text or dialogue bodies survive reduction; confirmed identity stays in memory.</summary>
public sealed class RunTracker
{
    private MapRun? run;
    private string areaId = "", location = "";
    private long seed, lastUptime;
    private DateTime lastTime;
    private bool active, naming;
    private long? baseline;
    private CopiedItem? nextMap;
    private string? character, clientSource;
    public bool CharacterConfirmed => character is not null;
    public long Revision { get; private set; }
    public void ConfirmCharacter(string? name) => character = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    public MapRun? Current => run;
    public void Notes(Guid id, string notes) { if (run?.Id == id) run = run with { Notes = notes }; }
    public void Copied(CopiedItem item)
    {
        if (item.ItemClass == "Maps" && item.MapTier is not null) nextMap = item;
    }
    public MapRun? Accept(ClientLogEvent entry, string league)
    {
        if (entry.DroppedBefore > 0 || entry.Event is LogSourceChanged || RunAreas.SessionEnded(entry.Event))
        {
            var incomplete = Close(true);
            nextMap = null; character = null; clientSource = null;
            // The event after a gap can establish a fresh instance, but never extends the old one.
            if (entry.Event is AreaGenerated) Accept(entry with { DroppedBefore = 0 }, league);
            return incomplete;
        }
        if (entry.Event.Envelope is not { } envelope) return null;
        var time = envelope.Timestamp;
        var uptime = envelope.UptimeMilliseconds;
        var sourceChanged = envelope.Source.StartsWith("Client ", StringComparison.Ordinal)
            && clientSource is not null && clientSource != envelope.Source;
        if (envelope.Source.StartsWith("Client ", StringComparison.Ordinal)) clientSource = envelope.Source;
        if (run is not null && (sourceChanged || uptime < lastUptime || time < lastTime))
        {
            var interrupted = Close(true); character = null; nextMap = null;
            if (entry.Event is AreaGenerated) Accept(entry, league);
            return interrupted;
        }
        if (run is not null && active)
            run = run with { Seconds = run.Seconds + Math.Max(0, (uptime - lastUptime) / 1000d) };
        lastUptime = uptime; lastTime = time;
        MapRun? finished = null;
        switch (entry.Event)
        {
            case AreaGenerated g:
                var wasActive = active;
                location = g.AreaId;
                if (RunAreas.Map(g.AreaId))
                {
                    if (run is null || areaId != g.AreaId || seed != g.Seed)
                    {
                        finished = Close(false);
                        areaId = g.AreaId; seed = g.Seed; baseline = null;
                        run = new(1, Guid.NewGuid(), league, g.AreaId, g.Level - 67, time, time,
                            0, 1, 0, null, [], nextMap?.Mods.Take(64).Select(m => m[..Math.Min(m.Length, 512)]).ToArray() ?? [], "", false);
                        nextMap = null; naming = true; Revision++;
                    }
                    else if (!active) { run = run with { Portals = run.Portals + 1 }; Revision++; }
                    active = true;
                }
                else active = run is not null && wasActive && !RunAreas.Safe(g.AreaId) && RunAreas.Side(g.AreaId);
                if (wasActive != active) Revision++;
                break;
            case AreaEntered e:
                // Entered names are display data only; generated IDs alone determine safety.
                if (run is not null && naming && location == areaId)
                { run = run with { Map = e.Name }; naming = false; Revision++; }
                break;
            case PlayerDied death when run is not null && active && character is not null && death.Name == character:
                run = run with { Deaths = run.Deaths + 1 }; Revision++; break;
            case KillCountReported k when run is not null && active:
                if (baseline is { } b && k.Count >= b) run = run with { Kills = k.Count - b };
                else { baseline = k.Count; run = run with { Kills = null }; }
                Revision++; break;
            case NpcDialogue npc when run is not null && active && location == areaId:
                if (Mechanic(npc.Npc) is { } mechanic && !run.Mechanics.Contains(mechanic))
                { run = run with { Mechanics = run.Mechanics.Append(mechanic).ToArray() }; Revision++; }
                break;
        }
        return finished;
    }
    private MapRun? Close(bool incomplete)
    {
        var result = run is null ? null : run with { Ended = lastTime, Incomplete = incomplete };
        if (run is not null) Revision++;
        run = null; active = false; naming = false; baseline = null;
        return result;
    }
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
