using System.Threading.Channels;

namespace ExileSight.Core.Logs;

/// <summary>Overflow drops the newest event without blocking the tail. Dispose discards pending work;
/// an already running callback may finish. Completion permits callers to await that callback.</summary>
public sealed class LogSubscription : IDisposable
{
    private readonly Channel<ClientLogEvent> queue;
    private readonly Action<LogSubscription> remove;
    private long dropped, failures;
    private string? lastFailure;
    private int disposed;
    public long DroppedEvents => Interlocked.Read(ref dropped);
    public long Failures => Interlocked.Read(ref failures);
    public string? LastFailureType => Volatile.Read(ref lastFailure);
    public Task Completion { get; }

    internal LogSubscription(Action<ClientLogEvent> handler, int capacity, Action<string>? report,
        Action<LogSubscription> remove)
    {
        this.remove = remove;
        queue = Channel.CreateBounded<ClientLogEvent>(new BoundedChannelOptions(capacity)
        { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, AllowSynchronousContinuations = false });
        Completion = Task.Run(async () =>
        {
            await foreach (var value in queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (Volatile.Read(ref disposed) != 0) break;
                try { handler(value); }
                catch (Exception error)
                {
                    Interlocked.Increment(ref failures);
                    Volatile.Write(ref lastFailure, error.GetType().Name);
                    try { report?.Invoke(error.GetType().Name); }
                    catch (Exception) { /* Diagnostic sinks cannot stop delivery. */ }
                }
            }
        });
    }
    internal void Enqueue(ClientLogEvent value)
    {
        if (!queue.Writer.TryWrite(value)) Interlocked.Increment(ref dropped);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        remove(this);
        queue.Writer.TryComplete();
    }
}
