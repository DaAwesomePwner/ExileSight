using ExileSight.Core.Logs;

namespace ExileSight.Features.Lab;

public sealed class LabModel(LabStore? store = null)
{
    private readonly object sync = new();
    private readonly Dictionary<string, (LabLayout Layout, LabTracker Tracker)> layouts = [];
    private string selected = "Normal";
    private LabTracker? activeTracker;
    public bool Visible { get; private set; }
    public event Action? Changed;
    public void Toggle() { lock (sync) Visible = !Visible; Changed?.Invoke(); }
    public void Hide() { lock (sync) Visible = false; Changed?.Invoke(); }
    public void RestoreLatest() => Select(store?.LatestDifficulty() ?? "Normal");
    public void Import(byte[] bytes)
    {
        lock (sync)
        {
            var next = store?.Save(bytes) ?? LabImport.Parse(bytes);
            selected = next.Difficulty;
            activeTracker = new(next);
            layouts[selected] = (next, activeTracker);
        }
        Changed?.Invoke();
    }
    public void Select(string difficulty)
    {
        lock (sync)
        {
            if (!LabImport.Difficulties.Contains(difficulty)) throw new ArgumentException("Unknown difficulty.");
            if (!layouts.ContainsKey(difficulty) && store?.Load(difficulty) is { } next)
                layouts[difficulty] = (next, new(next));
            selected = difficulty;
            // Browsing another map does not redirect log events away from an active run.
            if (activeTracker?.Active != true)
                activeTracker = layouts.TryGetValue(selected, out var state) ? state.Tracker : null;
        }
        Changed?.Invoke();
    }
    public void Accept(LogEvent log)
    {
        if (log is not AreaEntered area) return;
        lock (sync)
        {
            activeTracker?.Enter(area.Name);
            if (activeTracker?.Active != true && layouts.TryGetValue(selected, out var state))
                activeTracker = state.Tracker;
        }
        Changed?.Invoke();
    }
    public (LabLayout? Layout, string? RoomId, string Tracking, string Difficulty) Snapshot()
    {
        lock (sync)
        {
            if (!layouts.TryGetValue(selected, out var state)) return (null, null, "Not tracking", selected);
            var tracker = state.Tracker;
            return (state.Layout, tracker.Current?.Id, tracker.Uncertain ? "Uncertain — last known room" :
                tracker.AtPlaza ? "At Plaza — not yet in a room" : tracker.Current is null ? "Not tracking" : "Tracking: " + tracker.Current.Name, selected);
        }
    }
}
