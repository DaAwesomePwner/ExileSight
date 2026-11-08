using System.Diagnostics;

namespace ExileSight.Core.Input;

public enum InputStage
{
    HookDelay, LeaseAge, LeaseReevaluated, ReleaseCancelled, PressQueueAge,
    ClipboardSnapshot, ClipboardRead, ClipboardOpen, ClipboardRestore, ClipboardOffer,
    CopyWaitTimeout, PackLoadStart, PackLoadEnd, PackReclaimStart, PackReclaimEnd
}
public enum InputTimingOutcome { Observed, Success, Timeout, Failed }
public readonly record struct InputTimingEntry(long Timestamp, InputStage Stage, long Milliseconds, InputTimingOutcome Outcome);

/// <summary>Fixed-size numeric diagnostics only: no key, item, chat, or exception payloads.</summary>
public sealed class InputTiming
{
    public const int Capacity = 128;
    public static InputTiming Shared { get; } = new();
    private readonly object sync = new();
    private readonly InputTimingEntry[] entries = new InputTimingEntry[Capacity];
    private int next, count;
    public static void Record(InputStage stage, long milliseconds, InputTimingOutcome outcome = InputTimingOutcome.Observed) =>
        Shared.Add(new(Environment.TickCount64, stage, milliseconds, outcome));
    internal void RecordHook(HotkeyDispatcher dispatcher, PhysicalKey key, long now,
        long cachedAge, bool reevaluated, bool allowed)
    {
        var delay = now - key.Timestamp;
        if (!reevaluated && delay < 50 && !dispatcher.IsTimingRelevant(key)) return;
        var timestamp = Environment.TickCount64;
        Add(new(timestamp, InputStage.HookDelay, delay, InputTimingOutcome.Observed));
        Add(new(timestamp, InputStage.LeaseAge, cachedAge, InputTimingOutcome.Observed));
        if (reevaluated) Add(new(timestamp, InputStage.LeaseReevaluated, cachedAge,
            allowed ? InputTimingOutcome.Success : InputTimingOutcome.Failed));
    }
    public void Add(InputTimingEntry entry)
    {
        lock (sync)
        {
            entries[next] = entry;
            next = (next + 1) % Capacity;
            count = Math.Min(count + 1, Capacity);
        }
    }
    public InputTimingEntry[] Snapshot(int maximum = Capacity)
    {
        lock (sync)
        {
            var length = Math.Clamp(maximum, 0, count);
            var result = new InputTimingEntry[length];
            for (var i = 0; i < length; i++) result[i] = entries[(next - length + i + Capacity) % Capacity];
            return result;
        }
    }
    public static T Measure<T>(InputStage stage, Func<T> operation)
    {
        var start = Stopwatch.GetTimestamp();
        var outcome = InputTimingOutcome.Success;
        try { return operation(); }
        catch (TimeoutException) { outcome = InputTimingOutcome.Timeout; throw; }
        catch { outcome = InputTimingOutcome.Failed; throw; }
        finally { Record(stage, (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds, outcome); }
    }
}
