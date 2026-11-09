using ExileSight.Core.Logs;

namespace ExileSight.Features.Lab;

public sealed class LabModel(LabStore? store = null)
{
    private readonly object sync = new();
    private LabLayout? layout;
    private LabTracker? tracker;
    public bool Visible { get; private set; }
    public event Action? Changed;
    public void Toggle() { lock (sync) Visible = !Visible; Changed?.Invoke(); }
    public void Hide() { lock (sync) Visible = false; Changed?.Invoke(); }
    public void Import(byte[] bytes)
    {
        lock (sync) { var next = store?.Save(bytes) ?? LabImport.Parse(bytes); layout = next; tracker = new(next); }
        Changed?.Invoke();
    }
    public void Select(string difficulty)
    {
        lock (sync) { var next = store?.Load(difficulty); layout = next; tracker = next is null ? null : new(next); }
        Changed?.Invoke();
    }
    public void Accept(LogEvent log)
    {
        if (log is not AreaEntered area) return;
        lock (sync) tracker?.Enter(area.Name);
        Changed?.Invoke();
    }
    public (LabLayout? Layout, string? RoomId, string Tracking) Snapshot()
    {
        lock (sync) return (layout, tracker?.Current?.Id, tracker?.Current is null ? "Not tracking" :
            tracker.Uncertain ? "Uncertain — last known room" : "Tracking: " + tracker.Current.Name);
    }
}
