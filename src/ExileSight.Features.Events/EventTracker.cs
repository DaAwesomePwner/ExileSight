// Behaviour reference: Exile-UI (MIT), modules/client log.ahk 578–592 and
// modules/qol tools.ahk 757–771. Matching/first-approach filtering belongs to Core's parser.
using ExileSight.Core.Logs;

namespace ExileSight.Features.Events;

/// <summary>Pure ordered-delivery state machine; discontinuities require a fresh live instance.</summary>
public sealed class EventTracker
{
    private (string Id, long Seed)? area;
    private long? generation;
    private readonly HashSet<(string Area, long Seed, string Kind)> seen = [];
    public long Epoch { get; private set; }

    public static bool IsSupported(string id) => id is "MercenaryEncounter" or "Seer" or "ReflectingMist" or "Hideout";

    public static string? Map(LogEvent value) => value switch
    {
        MercenaryEncounter => "MercenaryEncounter",
        MapEvent { Kind: "Seer" or "ReflectingMist" or "Hideout" } map => map.Kind,
        _ => null
    };

    public string? Process(ClientLogEvent delivery)
    {
        if (delivery.IsReplay) return null;
        var sourceChanged = delivery.Event is LogSourceChanged ||
            generation is { } previous && previous != delivery.Generation;
        if (sourceChanged) seen.Clear();
        if (delivery.DroppedBefore > 0 || sourceChanged)
        {
            area = null; Epoch++;
        }
        generation = delivery.Generation;
        if (delivery.Event is AreaGenerated generated)
        {
            var next = (generated.AreaId, generated.Seed);
            if (area != next) { area = next; Epoch++; }
            return null;
        }
        var id = Map(delivery.Event);
        return area is { } instance && id is not null && seen.Add((instance.Id, instance.Seed, id)) ? id : null;
    }
}
