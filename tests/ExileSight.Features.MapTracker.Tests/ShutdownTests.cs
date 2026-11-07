using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Time;
using Xunit;
namespace ExileSight.Features.MapTracker.Tests;

public sealed class ShutdownTests
{
    [Theory]
    [InlineData(false, 25.5)]
    [InlineData(true, 10)]
    public async Task NormalExitFlushesThenPersistsFinalObservedAndUncheckpointedTime(bool paused, double expected)
    {
        var clock = new FakeClock(); var store = new TestStore(); var model = new TrackerModel(store, clock);
        model.SelectLeague("L"); model.Accept(TransitionTests.Area(0, "MapWorldsBeach")); await model.FlushAsync();
        clock.Milliseconds = 10000;
        model.Accept(paused ? TransitionTests.Area(10, "HideoutCoastal") :
            TransitionTests.E(new Unknown("", TransitionTests.At(10))));
        clock.Milliseconds = 25500;
        await model.StopAsync();
        var final = Assert.Single(store.Read("L"));
        Assert.Equal(expected, final.Seconds); Assert.True(final.Incomplete);
        Assert.Equal(TransitionTests.At(0).Timestamp.AddSeconds(expected), final.Ended);
        var writes = store.Writes.Count; await model.StopAsync();
        model.Accept(TransitionTests.Area(100, "MapWorldsDunes")); await model.FlushAsync();
        Assert.Equal(writes, store.Writes.Count);
    }
    [Fact]
    public async Task FinalCheckpointWaitsForInflightSnapshotAndUsesExitCutoff()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var clock = new FakeClock(); var first = true;
        var store = new TestStore { BeforeSave = _ =>
        {
            if (!first) return;
            first = false; entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
        } };
        var model = new TrackerModel(store, clock); model.SelectLeague("L");
        model.Accept(TransitionTests.Area(0, "MapWorldsBeach"));
        Task? stopping = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            clock.Milliseconds = 10000;
            model.Accept(TransitionTests.E(new Unknown("", TransitionTests.At(10))));
            clock.Milliseconds = 25000; stopping = model.StopAsync();
            Assert.False(stopping.IsCompleted);
            clock.Milliseconds = 99000; // Waiting on storage is not additional play time.
        }
        finally { release.Set(); await (stopping ?? model.StopAsync()); }
        Assert.Equal(2, store.Writes.Count);
        Assert.Equal(0, store.Writes[0].Seconds); Assert.Equal(25, store.Writes[1].Seconds);
    }
    [Fact]
    public async Task ExitDoesNotExtrapolateReplayTime()
    {
        var clock = new FakeClock(); var store = new TestStore(); var model = new TrackerModel(store, clock);
        model.SelectLeague("L"); model.Accept(TransitionTests.Area(0, "MapWorldsBeach") with { IsReplay = true });
        clock.Milliseconds = 10000; await model.StopAsync();
        Assert.Equal(0, Assert.Single(store.Read("L")).Seconds);
    }
    [Fact]
    public async Task FailureIsPerLeagueAndUnexpectedErrorsAreTypeOnlyAndDoNotFaultExit()
    {
        var errors = new List<string>();
        var store = new TestStore { BeforeSave = run => { if (run.League == "Bad") throw new InvalidOperationException("secret detail"); } };
        var model = new TrackerModel(store, new FakeClock(), errors.Add);
        model.SelectLeague("Bad"); model.Accept(TransitionTests.Area(0, "MapWorldsBeach")); await model.FlushAsync();
        Assert.Contains("read-only", model.Status); Assert.DoesNotContain("secret", model.Status);
        Assert.Equal(["InvalidOperationException"], errors);
        model.SelectLeague("Good"); Assert.DoesNotContain("read-only", model.Status);
        model.Accept(TransitionTests.Area(10, "MapWorldsDunes")); await model.FlushAsync();
        model.SelectLeague("Bad"); Assert.Contains("read-only", model.Status);
        model.SelectLeague("Good"); Assert.DoesNotContain("read-only", model.Status);
        await model.StopAsync();
        Assert.Single(store.Read("Good")); Assert.Empty(store.Read("Bad"));
    }
    [Fact]
    public async Task BrokenDiagnosticSinkCannotFaultDrainOrShutdown()
    {
        var store = new TestStore { BeforeSave = _ => throw new ArgumentException("private") };
        var model = new TrackerModel(store, new FakeClock(), _ => throw new Exception("logger failed"));
        model.SelectLeague("L"); model.Accept(TransitionTests.Area(0, "MapWorldsBeach"));
        await model.StopAsync(); Assert.Contains("ArgumentException", model.Status);
    }
    [Theory]
    [InlineData(false, 70)]
    [InlineData(true, 10)]
    public async Task HoursOfSilenceOrSleepCannotExtendExitBeyondOneWindow(bool paused, double expected)
    {
        var clock = new FakeClock(); var store = new TestStore(); var model = new TrackerModel(store, clock);
        model.SelectLeague("L"); model.Accept(TransitionTests.Area(0, "MapWorldsBeach"));
        clock.Milliseconds = 10000;
        model.Accept(paused ? TransitionTests.Area(10, "HideoutCoastal") :
            TransitionTests.E(new Unknown("", TransitionTests.At(10))));
        clock.Milliseconds = 6 * 60 * 60 * 1000; // Includes sleep, just like TickCount64.
        await model.StopAsync();
        var run = Assert.Single(store.Read("L"));
        Assert.Equal(expected, run.Seconds); Assert.Equal(TransitionTests.At((int)expected).Timestamp, run.Ended); Assert.True(run.Incomplete);
    }
    [Fact]
    public async Task SleepAcrossObservedCheckpointIsCountedFromLogUptimeWithoutClosingRun()
    {
        var clock = new FakeClock(); var store = new TestStore(); var model = new TrackerModel(store, clock);
        model.SelectLeague("L"); model.Accept(TransitionTests.Area(0, "MapWorldsBeach"));
        clock.Milliseconds = 60000;
        model.Accept(TransitionTests.E(new Unknown("", TransitionTests.At(60)))); await model.FlushAsync();
        var checkpoint = Assert.Single(store.Read("L"));
        Assert.Equal(60, checkpoint.Seconds);
        clock.Milliseconds = 6 * 60 * 60 * 1000; // Simulated sleep; no log evidence yet.
        await model.FlushAsync();
        Assert.Equal(checkpoint, Assert.Single(store.Read("L")));
        model.Accept(TransitionTests.E(new Unknown("", TransitionTests.At(21600)))); await model.FlushAsync();
        var resumed = Assert.Single(store.Read("L"));
        Assert.Equal(checkpoint.Id, resumed.Id);
        Assert.Equal(21600, resumed.Seconds); Assert.Equal(TransitionTests.At(21600).Timestamp, resumed.Ended);
        model.Accept(TransitionTests.Area(21610, "MapWorldsDunes")); await model.StopAsync();
        var completed = Assert.Single(store.Read("L"), r => r.Id == checkpoint.Id);
        Assert.Equal(21610, completed.Seconds); Assert.False(completed.Incomplete);
    }
    private sealed class FakeClock : IClock
    {
        public long Milliseconds { get; set; }
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch.AddMilliseconds(Milliseconds);
        public void Delay(int milliseconds) => Milliseconds += milliseconds;
    }
}
