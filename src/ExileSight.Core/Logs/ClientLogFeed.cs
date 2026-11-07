namespace ExileSight.Core.Logs;

/// <summary>Offset is a process-local event sequence, not a file byte offset. Generation changes with the path.</summary>
public sealed record ClientLogEvent(long Offset, long Generation, LogEvent Event, bool IsReplay)
{
    /// <summary>Number of live deliveries lost immediately before this event for this subscriber.</summary>
    public long DroppedBefore { get; init; }
    public DateTime? Timestamp => Event.Envelope?.Timestamp;
    public long? UptimeMilliseconds => Event.Envelope?.UptimeMilliseconds;
}

/// <summary>Path selection changed. The enclosing Generation identifies the new source without exposing its path.</summary>
public sealed record LogSourceChanged() : LogEvent("", null);

/// <summary>Replay only covers retained events observed by this live session, never pre-start file contents.
/// Both filters are inclusive; missing timestamps cannot match Since. At most MaxEvents are replayed.
/// Replay uses a separate bounded snapshot, reserving the full live capacity during catch-up.</summary>
public sealed record LogReplay(long? FromOffset = null, DateTime? Since = null, int MaxEvents = 256);

/// <summary>One publisher, bounded in-memory history and independent serial worker queues.
/// Replay and live registration are atomic. Path changes clear history but retain subscriptions.</summary>
public sealed class ClientLogFeed : IDisposable
{
    private readonly object sync = new();
    private readonly Queue<ClientLogEvent> history = new();
    private readonly List<LogSubscription> subscriptions = [];
    private readonly int historyCapacity;
    private long offset, generation;
    private bool disposed;
    public ClientLogFeed(int historyCapacity = 2048)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(historyCapacity, 1);
        this.historyCapacity = historyCapacity;
    }

    public LogSubscription Subscribe(Action<ClientLogEvent> handler, LogReplay? replay = null,
        int capacity = 256, Action<string>? reportFailure = null, string name = "anonymous")
    {
        ArgumentNullException.ThrowIfNull(handler);
        return SubscribeCore(_ => handler, replay, capacity, reportFailure, name);
    }

    internal LogSubscription SubscribeCore(Func<ClientLogEvent[], Action<ClientLogEvent>> createHandler,
        LogReplay? replay, int capacity, Action<string>? reportFailure, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        if (replay is not null) ArgumentOutOfRangeException.ThrowIfLessThan(replay.MaxEvents, 1);
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var retained = replay is null ? [] : history.Where(e =>
                (replay.FromOffset is null || e.Offset >= replay.FromOffset) &&
                (replay.Since is null || e.Timestamp >= replay.Since))
                .TakeLast(Math.Min(replay.MaxEvents, historyCapacity))
                .Select(e => e with { IsReplay = true }).ToArray();
            var subscription = new LogSubscription(createHandler(retained), retained, capacity, reportFailure, Remove, name);
            subscriptions.Add(subscription);
            return subscription;
        }
    }

    internal void Publish(LogEvent value)
    {
        lock (sync)
        {
            if (disposed) return;
            var entry = new ClientLogEvent(++offset, generation, value, false);
            history.Enqueue(entry);
            while (history.Count > historyCapacity) history.Dequeue();
            foreach (var subscriber in subscriptions) subscriber.Enqueue(entry);
        }
    }

    internal void ChangePath()
    {
        lock (sync)
        {
            generation++; history.Clear();
            var changed = new ClientLogEvent(++offset, generation, new LogSourceChanged(), false);
            foreach (var subscriber in subscriptions) subscriber.Enqueue(changed);
        }
    }
    private void Remove(LogSubscription subscription) { lock (sync) subscriptions.Remove(subscription); }
    public string Diagnostic
    {
        get
        {
            lock (sync) return string.Join("; ", subscriptions.Select(s =>
                $"{s.Name}: dropped {s.DroppedEvents} events; failures {s.Failures}; last type {s.LastFailureType ?? "none"}"));
        }
    }
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            foreach (var subscriber in subscriptions.ToArray()) subscriber.Dispose();
            history.Clear();
        }
    }
}
