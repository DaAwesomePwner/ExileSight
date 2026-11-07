namespace ExileSight.Core.Input;

/// <summary>Bounded single-producer/single-consumer ring. No locks or allocation on enqueue/dequeue.</summary>
public sealed class PressQueue
{
    private readonly PhysicalPress?[] entries = new PhysicalPress[65];
    private int read;
    private int write;
    public bool TryEnqueue(PhysicalPress press)
    {
        var next = (write + 1) % entries.Length;
        if (next == Volatile.Read(ref read)) return false;
        entries[write] = press;
        Volatile.Write(ref write, next);
        return true;
    }
    public bool TryDequeue(out PhysicalPress? press)
    {
        press = null;
        if (read == Volatile.Read(ref write)) return false;
        press = entries[read];
        entries[read] = null;
        Volatile.Write(ref read, (read + 1) % entries.Length);
        return true;
    }
}
