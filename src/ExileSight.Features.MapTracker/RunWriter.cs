namespace ExileSight.Features.MapTracker;

/// <summary>One off-feed writer. Coalesce pending snapshots by run, without an unbounded task chain.</summary>
public sealed class RunWriter(IRunStore store)
{
    private readonly object sync = new(), io = new();
    private readonly Dictionary<Guid, MapRun> pending = [];
    private readonly HashSet<string> blocked = [];
    private Task worker = Task.CompletedTask;
    private string? failure;
    public string? Failure => Volatile.Read(ref failure);
    public void Enqueue(MapRun run)
    {
        lock (sync)
        {
            if (blocked.Contains(run.League)) return;
            if (pending.Count >= 256 && !pending.ContainsKey(run.Id))
            {
                blocked.Add(run.League);
                Volatile.Write(ref failure, "Recording read-only: persistence queue full. New changes were not saved.");
                return;
            }
            pending[run.Id] = run;
            if (worker.IsCompleted) worker = Task.Run(Drain);
        }
    }
    private void Drain()
    {
        while (true)
        {
            MapRun run;
            lock (sync)
            {
                if (pending.Count == 0) { worker = Task.CompletedTask; return; }
                var first = pending.First(); run = first.Value; pending.Remove(first.Key);
                if (blocked.Contains(run.League)) continue;
            }
            try { lock (io) store.Save(run); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                lock (sync) blocked.Add(run.League);
                Volatile.Write(ref failure, "Recording read-only; new changes were not saved. " +
                    (e is IOException ? "Storage unavailable or history size cap reached." : e.GetType().Name));
            }
        }
    }
    public Task FlushAsync() { lock (sync) return worker; }
    public IReadOnlyList<MapRun> Read(string league) { lock (io) return store.Read(league); }
}
