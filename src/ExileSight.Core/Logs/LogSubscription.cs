using System.Threading.Channels;

namespace ExileSight.Core.Logs;

/// <summary>Replay is delivered first from a bounded snapshot; live overflow drops oldest.
/// Dispose discards pending work. Completion waits for an already running callback.</summary>
public sealed class LogSubscription : IDisposable
{
    private readonly Channel<(long Sequence, ClientLogEvent Event)> queue;
    private readonly Action<LogSubscription> remove;
    private long sequence, dropped, failures;
    private string? lastFailure;
    private int disposed;
    public string Name { get; }
    public long DroppedEvents => Interlocked.Read(ref dropped);
    public long Failures => Interlocked.Read(ref failures);
    public string? LastFailureType => Volatile.Read(ref lastFailure);
    public Task Completion { get; }

    internal LogSubscription(Action<ClientLogEvent> handler, ClientLogEvent[] replay, int capacity,
        Action<string>? report, Action<LogSubscription> remove, string name)
    {
        this.remove = remove;
        Name = name;
        queue = Channel.CreateBounded<(long, ClientLogEvent)>(new BoundedChannelOptions(capacity)
        { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, AllowSynchronousContinuations = false },
            _ => Interlocked.Increment(ref dropped));
        Completion = Task.Run(async () =>
        {
            void Deliver(ClientLogEvent value)
            {
                try { handler(value); }
                catch (Exception error)
                {
                    var errors = error is LogHandlerFailures group ? group.Errors : [error];
                    foreach (var failure in errors)
                    {
                        Interlocked.Increment(ref failures);
                        Volatile.Write(ref lastFailure, failure.GetType().Name);
                        try { report?.Invoke(failure.GetType().Name); }
                        catch (Exception) { /* Diagnostic sinks cannot stop delivery. */ }
                    }
                }
            }
            foreach (var value in replay)
            {
                if (Volatile.Read(ref disposed) != 0) return;
                Deliver(value);
            }
            long previous = 0;
            await foreach (var entry in queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (Volatile.Read(ref disposed) != 0) break;
                Deliver(entry.Event with { DroppedBefore = entry.Sequence - previous - 1 });
                previous = entry.Sequence;
            }
        });
    }
    // The feed serializes writers. Sequence gaps report exactly the drops preceding this delivery.
    internal void Enqueue(ClientLogEvent value) => queue.Writer.TryWrite((++sequence, value));
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        remove(this);
        queue.Writer.TryComplete();
    }
}

internal sealed class LogHandlerFailures(List<Exception> errors) : Exception
{
    internal List<Exception> Errors { get; } = errors;
}
