namespace ExileSight.Core.Input;

internal sealed class ForegroundLease
{
    internal const long MaximumAgeMilliseconds = 250;
    private sealed record Snapshot(nint Handle, bool Focused, long Timestamp);
    private Snapshot current = new(0, false, long.MinValue);
    internal void Publish(nint handle, bool focused, long timestamp) =>
        Volatile.Write(ref current, new(handle, focused, timestamp));
    internal long Age(long now)
    {
        var timestamp = Volatile.Read(ref current).Timestamp;
        return timestamp == long.MinValue ? -1 : now - timestamp;
    }
    internal bool IsStale(long now)
    {
        var timestamp = Volatile.Read(ref current).Timestamp;
        return timestamp == long.MinValue || now < timestamp || now - timestamp > MaximumAgeMilliseconds;
    }
    internal bool AllowsBlocking(long now, nint foreground)
    {
        var state = Volatile.Read(ref current);
        return state.Focused && state.Handle != 0 && state.Handle == foreground && now >= state.Timestamp &&
            now - state.Timestamp <= MaximumAgeMilliseconds;
    }
}
