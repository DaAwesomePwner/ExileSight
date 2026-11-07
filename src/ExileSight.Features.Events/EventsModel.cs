// ExileSight-authored passive presentation; Exile-UI behaviour references are in EventTracker.cs.
using ExileSight.Core.DataPacks;
using ExileSight.Core.Logs;

namespace ExileSight.Features.Events;

public sealed record EventOption(EventDefinition Event, bool Enabled);

/// <summary>Thread-safe, pack-only display data. One five-second toast and at most four pending.
/// Pending entries expire after fifteen seconds. No log text is retained.</summary>
public sealed class EventsModel(Func<long>? clock = null, Action<string, bool>? save = null)
{
    private readonly object sync = new();
    private readonly Func<long> now = clock ?? (() => Environment.TickCount64);
    private readonly EventTracker tracker = new();
    private readonly Queue<(EventDefinition Event, long At)> queue = new();
    private Dictionary<string, EventOption> options = new(StringComparer.Ordinal);
    private EventDefinition? current;
    private long until;
    public event Action? SettingsRequested;
    public void OpenSettings() => SettingsRequested?.Invoke();
    public EventOption[] Options { get { lock (sync) return options.Values.ToArray(); } }
    public int PendingCount { get { lock (sync) { Advance(); return queue.Count; } } }
    public EventDefinition? Current { get { lock (sync) { Advance(); return current; } } }

    public void Configure(IReadOnlyList<EventDefinition>? definitions, Func<string, bool?> read)
    {
        lock (sync)
        {
            options = (definitions ?? []).Where(d => EventTracker.IsSupported(d.Id)).ToDictionary(d => d.Id,
                    d => new EventOption(d, read(d.Id) ?? d.EnabledByDefault), StringComparer.Ordinal);
            // Never carry presentation from an obsolete pack snapshot.
            queue.Clear(); current = null;
        }
    }

    public void SetEnabled(string id, bool enabled)
    {
        lock (sync)
        {
            if (!options.TryGetValue(id, out var option)) throw new ArgumentException("Unknown event", nameof(id));
            save?.Invoke(id, enabled); // Persist before exposing the change.
            options[id] = option with { Enabled = enabled };
            if (!enabled)
            {
                var retained = queue.Where(q => q.Event.Id != id).ToArray();
                queue.Clear(); foreach (var entry in retained) queue.Enqueue(entry);
                if (current?.Id == id) current = null;
                Advance();
            }
        }
    }

    public void Accept(ClientLogEvent delivery)
    {
        lock (sync)
        {
            var epoch = tracker.Epoch;
            var id = tracker.Process(delivery);
            if (epoch != tracker.Epoch) { queue.Clear(); current = null; }
            Advance();
            if (id is null || !options.TryGetValue(id, out var option) || !option.Enabled) return;
            if (current is null) { current = option.Event; until = now() + 5000; }
            else if (queue.Count < 4) queue.Enqueue((option.Event, now()));
        }
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
