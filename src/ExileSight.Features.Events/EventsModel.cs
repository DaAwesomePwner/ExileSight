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
    private readonly EventToastQueue toasts = new(clock ?? (() => Environment.TickCount64));
    private readonly EventTracker tracker = new();
    private Dictionary<string, EventOption> options = new(StringComparer.Ordinal);
    public event Action? SettingsRequested;
    public void OpenSettings() => SettingsRequested?.Invoke();
    public EventOption[] Options { get { lock (sync) return options.Values.ToArray(); } }
    public int PendingCount { get { lock (sync) return toasts.PendingCount; } }
    public EventDefinition? Current { get { lock (sync) return toasts.Current; } }

    public void Configure(IReadOnlyList<EventDefinition>? definitions, Func<string, bool?> read)
    {
        lock (sync)
        {
            options = (definitions ?? []).Where(d => EventTracker.IsSupported(d.Id)).ToDictionary(d => d.Id,
                    d => new EventOption(d, read(d.Id) ?? d.EnabledByDefault), StringComparer.Ordinal);
            // Never carry presentation from an obsolete pack snapshot.
            toasts.Clear();
        }
    }

    public void SetEnabled(string id, bool enabled)
    {
        lock (sync)
        {
            if (!options.TryGetValue(id, out var option)) throw new ArgumentException("Unknown event", nameof(id));
            save?.Invoke(id, enabled); // Persist before exposing the change.
            options[id] = option with { Enabled = enabled };
            if (!enabled) toasts.Remove(id);
        }
    }

    public void Accept(ClientLogEvent delivery)
    {
        lock (sync)
        {
            var epoch = tracker.Epoch;
            var id = tracker.Process(delivery);
            if (epoch != tracker.Epoch) toasts.Clear();
            if (id is null || !options.TryGetValue(id, out var option) || !option.Enabled) return;
            toasts.Enqueue(option.Event);
        }
    }
}
