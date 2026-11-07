using System.Collections.Concurrent;

namespace ExileSight.Core.Input;

public sealed record PassiveInput(uint Message, int X, int Y, long Timestamp);

/// <summary>Hook producer, UI consumer. Hidden overlays allocate/enqueue nothing.
/// Moves occupy one latest-only slot; clicks/Esc have a separate lossless queue.
/// A visibility epoch prevents old observations from dismissing a reopened overlay.</summary>
public sealed class PassiveInputChannel
{
    private sealed class Interest(long openedAt)
    {
        public long OpenedAt { get; } = openedAt;
        public readonly ConcurrentQueue<PassiveInput> Discrete = new();
        public PassiveInput? Move;
    }
    private Interest? interest;
    public event Action<PassiveInput>? Observed;
    public void Open(long timestamp) => Volatile.Write(ref interest, new(timestamp));
    public void Close() => Volatile.Write(ref interest, null);
    public bool IsInterested => Volatile.Read(ref interest) is not null;
    public void Observe(uint message, int x, int y, long timestamp)
    {
        var current = Volatile.Read(ref interest);
        if (current is null || timestamp < current.OpenedAt) return;
        if (message == 0x200)
            Interlocked.Exchange(ref current.Move, new(message, x, y, timestamp));
        else if (message is 0x100 or 0x201 or 0x204 or 0x207 or 0x20b)
            current.Discrete.Enqueue(new(message, x, y, timestamp));
    }
    // Called by a UI dispatcher tick, never by the action worker.
    public void Drain()
    {
        var current = Volatile.Read(ref interest);
        if (current is null) return;
        while (current.Discrete.TryDequeue(out var input))
        {
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
            catch (Exception) { /* Passive observers cannot compromise input or other observers. */ }
        }
    }
}
