namespace ExileSight.Core.Input;

public sealed record OverlayClickTarget(nint Window, int Left, int Top, int Right, int Bottom, int ZOrder = 0);
internal sealed record PhysicalClick(nint Window, int X, int Y, long Timestamp);

/// <summary>Fixed-size single-hook-producer mailbox; no locks, waits or window queries on the hook.</summary>
public sealed class OverlayClickPairing
{
    private OverlayClickTarget[] targets = [];
    private PhysicalClick? down;
    private PhysicalClick? pending;
    public void SetTargets(IEnumerable<OverlayClickTarget> windows)
    {
        var next = windows.Take(33).OrderBy(w => w.ZOrder).ToArray();
        if (next.Length > 32 || next.Any(w => w.Window == 0)) throw new ArgumentException("Invalid overlay snapshot");
        if (Volatile.Read(ref targets).SequenceEqual(next)) return;
        Volatile.Write(ref targets, next);
        Interlocked.Exchange(ref pending, null);
    }
    internal void Observe(bool isDown, bool injected, int x, int y, long timestamp)
    {
        // Injected events must never preserve a partial physical gesture.
        if (injected) { down = null; Interlocked.Exchange(ref pending, null); return; }
        OverlayClickTarget? target = null;
        foreach (var window in Volatile.Read(ref targets))
            if (x >= window.Left && x < window.Right && y >= window.Top && y < window.Bottom)
            { target = window; break; }
        if (isDown)
        {
            Interlocked.Exchange(ref pending, null);
            down = target is null ? null : new(target.Window, x, y, timestamp);
            return;
        }
        var start = down; down = null;
        if (target is null || start is null || target.Window != start.Window || timestamp < start.Timestamp ||
            timestamp - start.Timestamp > 1500 || Math.Abs((long)x - start.X) > 4 || Math.Abs((long)y - start.Y) > 4) return;
        Interlocked.Exchange(ref pending, new(target.Window, x, y, timestamp));
    }
    internal PhysicalClick? Redeem(nint window, int x, int y, long now)
    {
        var click = Interlocked.Exchange(ref pending, null);
        return click is not null && click.Window == window && now >= click.Timestamp && now - click.Timestamp <= 500 &&
            Math.Abs((long)x - click.X) <= 4 && Math.Abs((long)y - click.Y) <= 4 &&
            Volatile.Read(ref targets).Any(t => t.Window == window && x >= t.Left && x < t.Right && y >= t.Top && y < t.Bottom)
            ? click : null;
    }
}
