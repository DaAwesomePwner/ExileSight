using System.Collections.Concurrent;

namespace ExileSight.Core.Input;

public sealed record PassiveInput(uint Message, int X, int Y, long Timestamp);

/// <summary>Hook producer, UI consumer. Hidden overlays allocate/enqueue nothing.
/// Moves occupy one latest-only slot; clicks/Esc have a bounded queue that fails closed on overflow.
/// A visibility epoch prevents old observations from dismissing a reopened overlay.</summary>
public sealed class PassiveInputChannel
{
    public const int Capacity = 128;
    public Action<string> ReportFailure { get; set; } = type => System.Diagnostics.Trace.TraceWarning("Passive observer failed: {0}", type);
    private sealed class Interest(long openedAt)
    {
        public long OpenedAt { get; } = openedAt;
        public readonly ConcurrentQueue<PassiveInput> Discrete = new();
        public PassiveInput? Move;
        public int Count;
        public PassiveInput? Overflow;
    }
    private Interest? interest;
    public event Action<PassiveInput>? Observed;
    public void Open(long timestamp) => Volatile.Write(ref interest, new(timestamp));
    public void Close() => Volatile.Write(ref interest, null);
    public bool IsInterested => Volatile.Read(ref interest) is not null;
    public void Observe(uint message, int x, int y, long timestamp)
    {
        var current = Volatile.Read(ref interest);
        if (current is null || timestamp < current.OpenedAt || Volatile.Read(ref current.Overflow) is not null) return;
        if (message == 0x200)
            Interlocked.Exchange(ref current.Move, new(message, x, y, timestamp));
        else if (message is 0x100 or 0x201 or 0x204 or 0x207 or 0x20b)
        {
            if (Interlocked.Increment(ref current.Count) > Capacity)
            {
                Interlocked.Decrement(ref current.Count);
                Interlocked.CompareExchange(ref current.Overflow, new(0x100, 0, 0, timestamp), null);
            }
            else current.Discrete.Enqueue(new(message, x, y, timestamp));
        }
    }
    // Called by a UI dispatcher tick, never by the action worker.
    public void Drain()
    {
        var current = Volatile.Read(ref interest);
        if (current is null) return;
        if (Volatile.Read(ref current.Overflow) is { } overflow)
        {
            // An Esc observation closes every interested overlay without creating input or tokens.
            Publish(overflow, current);
            // Keep observing for overlays opened after the overflow timestamp.
            // A subscriber that closed/reopened the lease during Publish wins the exchange.
            Interlocked.CompareExchange(ref interest, new(current.OpenedAt), current);
            return;
        }
        while (current.Discrete.TryDequeue(out var input))
        {
            Interlocked.Decrement(ref current.Count);
            if (!ReferenceEquals(current, Volatile.Read(ref interest))) return;
            Publish(input, current);
        }
        var move = Interlocked.Exchange(ref current.Move, null);
        if (move is not null && ReferenceEquals(current, Volatile.Read(ref interest))) Publish(move, current);
    }
    private void Publish(PassiveInput input, Interest current)
    {
        if (Observed is not { } handlers) return;
        foreach (Action<PassiveInput> handler in handlers.GetInvocationList())
        {
            if (!ReferenceEquals(current, Volatile.Read(ref interest))) return;
            try { handler(input); }
            catch (Exception error)
            {
                try { ReportFailure(error.GetType().Name); }
                catch (Exception) { /* Diagnostics cannot stop dismissal. */ }
            }
        }
    }
}
