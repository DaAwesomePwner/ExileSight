// ExileSight-authored passive toast scheduling. Callers serialize access (EventsModel holds its lock).
using ExileSight.Core.DataPacks;

namespace ExileSight.Features.Events;

/// <summary>One five-second toast, four pending maximum, and a fifteen-second pending lifetime.</summary>
public sealed class EventToastQueue(Func<long> now)
{
    private readonly Queue<(EventDefinition Event, long At)> queue = new();
    private EventDefinition? current;
    private long until;
    public EventDefinition? Current { get { Advance(); return current; } }
    public int PendingCount { get { Advance(); return queue.Count; } }

    public void Enqueue(EventDefinition definition)
    {
        Advance();
        if (current is null) { current = definition; until = now() + 5000; }
        else if (queue.Count < 4) queue.Enqueue((definition, now()));
    }

    public void Clear() { queue.Clear(); current = null; }

    public void Remove(string id)
    {
        var retained = queue.Where(q => q.Event.Id != id).ToArray();
        queue.Clear(); foreach (var entry in retained) queue.Enqueue(entry);
        if (current?.Id == id) current = null;
        Advance();
    }

    private void Advance()
    {
        var time = now();
        if (current is not null && time >= until) current = null;
        while (queue.TryPeek(out var entry) && time - entry.At >= 15000) queue.Dequeue();
        if (current is null && queue.TryDequeue(out var next))
        { current = next.Event; until = time + 5000; }
    }
}
