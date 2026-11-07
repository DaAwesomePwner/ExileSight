namespace ExileSight.Core.Tests.Settings;

/// <summary>Single-store clock: advancing time runs due one-shot saves on the test thread.</summary>
internal sealed class ManualSaveTime : TimeProvider
{
    private long timestamp;
    private SaveTimer? timer;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => timestamp;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(timestamp);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (timer is not null) throw new InvalidOperationException("Use one clock per store.");
        timer = new SaveTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        var target = checked(timestamp + elapsed.Ticks);
        while (timer?.Due is { } due && due <= target)
        {
            timestamp = due;
            timer.Fire();
        }
        timestamp = target;
    }

    private sealed class SaveTimer(ManualSaveTime clock, TimerCallback callback, object? state) : ITimer
    {
        private bool disposed;
        internal long? Due { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (disposed) return false;
            if (period != Timeout.InfiniteTimeSpan)
                throw new NotSupportedException("Settings saves must be one-shot timers.");
            if (dueTime != Timeout.InfiniteTimeSpan)
                ArgumentOutOfRangeException.ThrowIfLessThan(dueTime, TimeSpan.Zero);
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : checked(clock.timestamp + dueTime.Ticks);
            return true;
        }
        internal void Fire()
        {
            Due = null;
            callback(state);
        }
        public void Dispose() { disposed = true; Due = null; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
