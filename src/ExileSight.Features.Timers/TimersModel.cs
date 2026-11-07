using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Features.Timers;

public readonly record struct TimersVisibility(bool Visible, bool Interactive);

public sealed class TimersModel(TimerStore store, IClock clock)
{
    private readonly object sync = new();
    private long? held;
    private bool toggled;
    public bool Interactive { get { lock (sync) return toggled; } }
    public bool Visible { get { lock (sync) return toggled || held is not null; } }
    public string? Notice => store.Notice;
    public TimerState[] Snapshot() => store.Snapshot();
    public TimeSpan Value(Guid id) => Snapshot().Single(r => r.Id == id).Value(clock.UtcNow);
    public event Action<TimersVisibility>? VisibilityChanged;
    public void Hold(long id) => ChangeVisibility(() => held = id);
    public void Release(long id) => ChangeVisibility(() => { if (held == id) held = null; });
    public void Toggle() => ChangeVisibility(() => toggled = !toggled);
    public void LoseFocus() => ChangeVisibility(() => { held = null; toggled = false; });
    private void ChangeVisibility(Action change)
    {
        lock (sync)
        {
            var before = new TimersVisibility(toggled || held is not null, toggled);
            change();
            var after = new TimersVisibility(toggled || held is not null, toggled);
            if (before != after) VisibilityChanged?.Invoke(after);
        }
    }
    public Guid Add(string name, TimerKind kind, TimeSpan duration, bool alert)
    {
        lock (sync)
        {
            var rows = store.Snapshot();
            if (rows.Length >= TimerStore.MaxTimers) throw new InvalidOperationException("Maximum 50 timers.");
            var row = TimerState.Create(name, kind, duration, alert);
            store.Save([.. rows, row]); return row.Id;
        }
    }
    public void Rename(Guid id, string name) => Update(id, row => row.Rename(name));
    public void Start(Guid id) => Update(id, row => row.Start(clock.UtcNow));
    public void Pause(Guid id) => Update(id, row => row.Pause(clock.UtcNow));
    public void Reset(Guid id) => Update(id, row => row.Reset());
    public void SetAlert(Guid id, bool enabled) => Update(id, row => row with { Alert = enabled });
    public void Delete(Guid id) { lock (sync) store.Save(store.Snapshot().Where(r => r.Id != id).ToArray()); }
    public TimerState[] Tick()
    {
        lock (sync)
        {
            var rows = store.Snapshot(); var now = clock.UtcNow;
            var next = rows.Select(r => r.Complete(now)).ToArray();
            var completed = next.Where((r, i) => r.Alerted && !rows[i].Alerted).ToArray();
            if (completed.Length != 0) store.Save(next); // Commit before notifying; never repeat on restart.
            return completed.Where(r => r.Alert).ToArray();
        }
    }
    private void Update(Guid id, Func<TimerState, TimerState> change)
    { lock (sync) store.Save(store.Snapshot().Select(r => r.Id == id ? change(r) : r).ToArray()); }
}
