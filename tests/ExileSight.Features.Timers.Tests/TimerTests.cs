using ExileSight.Platform.Abstractions.Time;
using Xunit;

namespace ExileSight.Features.Timers.Tests;

public sealed class TimerTests
{
    [Theory]
    [InlineData(TimerKind.Countdown)]
    [InlineData(TimerKind.Stopwatch)]
    public void PauseResumeResetAndRestart(TimerKind kind)
    {
        using var folder = new TestFolder(); var clock = new Clock();
        var model = new TimersModel(new TimerStore(folder.Path), clock);
        var id = model.Add("Test", kind, TimeSpan.FromSeconds(60), true);
        model.Start(id); clock.Advance(10);
        model.Pause(id); clock.Advance(20);
        Assert.Equal(kind == TimerKind.Countdown ? 50 : 10, model.Value(id).TotalSeconds);
        model.Start(id); clock.Advance(5);
        var restarted = new TimersModel(new TimerStore(folder.Path), clock);
        Assert.Equal(kind == TimerKind.Countdown ? 45 : 15, restarted.Value(id).TotalSeconds);
        restarted.Reset(id);
        Assert.Equal(kind == TimerKind.Countdown ? 60 : 0, restarted.Value(id).TotalSeconds);
        Assert.Null(Assert.Single(restarted.Snapshot()).RunningSinceUtc);
    }
    [Fact]
    public void ExpiryIsPersistedBeforeAlertAndNeverRepeatedAfterRestart()
    {
        using var folder = new TestFolder(); var clock = new Clock();
        var model = new TimersModel(new TimerStore(folder.Path), clock);
        var id = model.Add("Tea", TimerKind.Countdown, TimeSpan.FromSeconds(5), true);
        model.Start(id); clock.Advance(8);
        Assert.Equal("Tea", Assert.Single(model.Tick()).Name);
        Assert.Empty(model.Tick());
        Assert.Empty(new TimersModel(new TimerStore(folder.Path), clock).Tick());
        model.Reset(id); model.Start(id); clock.Advance(5);
        Assert.Single(model.Tick());
    }
    [Fact]
    public void ClockJumpsClampAtZeroAndCountdownDurationButHonorUtcDeadline()
    {
        var start = DateTimeOffset.UnixEpoch;
        var count = TimerState.Create("Count", TimerKind.Countdown, TimeSpan.FromSeconds(60), true).Start(start);
        var watch = TimerState.Create("Watch", TimerKind.Stopwatch, TimeSpan.Zero, false).Start(start);
        Assert.Equal(TimeSpan.FromSeconds(60), count.Value(start.AddHours(-1)));
        Assert.Equal(TimeSpan.Zero, watch.Value(start.AddHours(-1)));
        Assert.Equal(TimeSpan.Zero, count.Value(start.AddHours(1)));
        Assert.Equal(TimeSpan.FromHours(1), watch.Value(start.AddHours(1)));
        Assert.True(count.Complete(start.AddHours(1)).Alerted);
    }
    [Fact]
    public void ExpiredWhileClosedAlertsOnceOnRestart()
    {
        using var folder = new TestFolder(); var clock = new Clock();
        var model = new TimersModel(new TimerStore(folder.Path), clock);
        var id = model.Add("Offline", TimerKind.Countdown, TimeSpan.FromSeconds(1), true);
        model.Start(id); clock.Advance(100);
        var restart = new TimersModel(new TimerStore(folder.Path), clock);
        Assert.Single(restart.Tick()); Assert.Empty(restart.Tick());
    }
    [Fact]
    public void AlertWaitsForSuccessfulPersistence()
    {
        using var folder = new TestFolder(); var clock = new Clock();
        var model = new TimersModel(new TimerStore(folder.Path), clock);
        var id = model.Add("Tea", TimerKind.Countdown, TimeSpan.FromSeconds(1), true);
        model.Start(id); clock.Advance(2);
        var temp = Path.Combine(folder.Path, "timers.json.tmp"); Directory.CreateDirectory(temp);
        var error = Record.Exception(() => model.Tick());
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.False(Assert.Single(model.Snapshot()).Alerted);
        Directory.Delete(temp); Assert.Single(model.Tick());
        Assert.Empty(new TimersModel(new TimerStore(folder.Path), clock).Tick());
    }
    [Fact]
    public void StopwatchCapsAtTenYearsEvenAcrossPauseResumeAndRestart()
    {
        using var folder = new TestFolder(); var clock = new Clock();
        var model = new TimersModel(new TimerStore(folder.Path), clock);
        var id = model.Add("Watch", TimerKind.Stopwatch, TimeSpan.Zero, false);
        model.Start(id); clock.Advance((int)TimeSpan.FromDays(3651).TotalSeconds);
        Assert.Equal(TimerState.Maximum, model.Value(id)); model.Pause(id); model.Start(id);
        clock.Advance(60); Assert.Equal(TimerState.Maximum, model.Value(id));
        Assert.Equal(TimerState.Maximum, new TimersModel(new TimerStore(folder.Path), clock).Value(id));
    }
    [Theory]
    [InlineData(TimerKind.Countdown)]
    [InlineData(TimerKind.Stopwatch)]
    public void StoredTimerClampsBackwardClockJump(TimerKind kind)
    {
        using var folder = new TestFolder(); var clock = new Clock();
        var model = new TimersModel(new TimerStore(folder.Path), clock);
        var id = model.Add("Jump", kind, TimeSpan.FromSeconds(60), true);
        model.Start(id); clock.Advance(10); model.Pause(id); model.Start(id);
        clock.Advance(-3600);
        var restart = new TimersModel(new TimerStore(folder.Path), clock);
        Assert.Equal(TimeSpan.FromSeconds(kind == TimerKind.Countdown ? 50 : 10), restart.Value(id));
        Assert.Empty(restart.Tick());
    }
    [Fact]
    public void PauseAfterExpiryBeforeTickStillAlertsOnce()
    {
        using var folder = new TestFolder(); var clock = new Clock();
        var model = new TimersModel(new TimerStore(folder.Path), clock);
        var id = model.Add("Tea", TimerKind.Countdown, TimeSpan.FromSeconds(1), true);
        model.Start(id); clock.Advance(2); model.Pause(id);
        Assert.False(Assert.Single(model.Snapshot()).IsRunning);
        Assert.Single(model.Tick()); Assert.Empty(model.Tick());
    }
    [Fact]
    public void QuietExpiryAndEditingRemainLocalAndBounded()
    {
        using var folder = new TestFolder(); var clock = new Clock();
        var model = new TimersModel(new TimerStore(folder.Path), clock);
        var id = model.Add("Quiet", TimerKind.Countdown, TimeSpan.FromSeconds(1), false);
        model.Rename(id, "Renamed"); model.Start(id); clock.Advance(2);
        Assert.Empty(model.Tick()); Assert.True(Assert.Single(model.Snapshot()).Alerted);
        model.Delete(id); Assert.Empty(model.Snapshot());
        for (var i = 0; i < TimerStore.MaxTimers; i++) model.Add("Watch", TimerKind.Stopwatch, TimeSpan.Zero, false);
        Assert.Throws<InvalidOperationException>(() => model.Add("Overflow", TimerKind.Stopwatch, TimeSpan.Zero, false));
    }
}
internal sealed class Clock : IClock
{
    public long Milliseconds { get; private set; }
    public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UnixEpoch;
    public void Advance(int seconds) { UtcNow = UtcNow.AddSeconds(seconds); Milliseconds += seconds * 1000L; }
    public void Delay(int milliseconds) => throw new NotSupportedException();
}
internal sealed class TestFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public TestFolder() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, true);
}
