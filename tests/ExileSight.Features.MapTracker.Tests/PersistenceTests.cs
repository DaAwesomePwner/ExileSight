using ExileSight.Core.Logs;
using Xunit;
namespace ExileSight.Features.MapTracker.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task ChatAndOtherNonStateEventsDoNotWriteUntilCoarseCheckpoint()
    {
        var store = new MemoryStore(); var model = new TrackerModel(store); model.SelectLeague("L");
        model.Accept(TransitionTests.Area(0, "MapWorldsBeach")); await model.FlushAsync();
        var writes = store.Writes;
        for (var i = 1; i < 60; i++)
            model.Accept(TransitionTests.E(new ChatMessage("", TransitionTests.At(i), "Local", "Private", "Body")));
        await model.FlushAsync(); Assert.Equal(writes, store.Writes);
        model.Accept(TransitionTests.E(new Unknown("", TransitionTests.At(60)))); await model.FlushAsync();
        Assert.Equal(writes + 1, store.Writes); Assert.Equal(60, Assert.Single(store.Read("L")).Seconds);
    }
    [Fact]
    public async Task StateChangesWriteWithoutWaitingForCheckpoint()
    {
        var store = new MemoryStore(); var model = new TrackerModel(store); model.SelectLeague("L"); model.ConfirmCharacter("Self");
        var events = new[] {
            TransitionTests.Area(0, "MapWorldsBeach"),
            TransitionTests.E(new PlayerSlain("", TransitionTests.At(1), "Self")),
            TransitionTests.E(new KillCountReported("", TransitionTests.At(2), 10)),
            TransitionTests.E(new KillCountReported("", TransitionTests.At(3), 20)),
            TransitionTests.E(new NpcDialogue("", TransitionTests.At(4), "Sister Cassia", "")),
            TransitionTests.Area(5, "HideoutCoastal"), TransitionTests.Area(6, "MapWorldsBeach") };
        foreach (var entry in events)
        {
            var count = store.Writes; model.Accept(entry); await model.FlushAsync(); Assert.Equal(count + 1, store.Writes);
        }
    }
    [Fact]
    public async Task LeagueSwitchClosesOldRunAndKeepsLeaguesSeparate()
    {
        var store = new MemoryStore(); var model = new TrackerModel(store); model.SelectLeague("L");
        model.Accept(TransitionTests.Area(0, "MapWorldsBeach"));
        model.Accept(TransitionTests.E(new Unknown("", TransitionTests.At(5))));
        model.SelectLeague("Other"); model.Accept(TransitionTests.Area(100, "MapWorldsDunes"));
        await model.FlushAsync();
        var old = Assert.Single(store.Read("L")); Assert.True(old.Incomplete); Assert.Equal(5, old.Seconds);
        Assert.Equal(0, Assert.Single(store.Read("Other")).Seconds);
    }
    [Fact]
    public async Task SlowDiskCannotBlockLogOrCopyLockAndPendingSnapshotsCoalesce()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var store = new MemoryStore { BeforeSave = () => { entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(10))); } };
        var model = new TrackerModel(store); model.SelectLeague("L");
        model.Accept(TransitionTests.Area(0, "MapWorldsBeach"));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            var delivery = Task.Run(() =>
            {
                for (var i = 1; i <= 1000; i++) model.Accept(TransitionTests.E(new KillCountReported("", TransitionTests.At(i), i)));
                model.Copied(new("Maps", "map", null, 1, []));
            });
            await delivery.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { release.Set(); await model.FlushAsync(); }
        Assert.Equal(2, store.Writes);
    }
    [Fact]
    public async Task StorageFailureMakesUnsavedChangesVisible()
    {
        var model = new TrackerModel(new MemoryStore { BeforeSave = () => throw new IOException() });
        model.SelectLeague("L"); model.Accept(TransitionTests.Area(0, "MapWorldsBeach")); await model.FlushAsync();
        Assert.Contains("read-only", model.Status); Assert.Contains("not saved", model.Status);
    }
    private sealed class MemoryStore : IRunStore
    {
        private readonly Dictionary<Guid, MapRun> rows = [];
        public int Writes { get; private set; }
        public Action? BeforeSave { get; init; }
        public IReadOnlyList<MapRun> Read(string league) => rows.Values.Where(r => r.League == league).ToArray();
        public void Save(MapRun run) { BeforeSave?.Invoke(); rows[run.Id] = run; Writes++; }
    }
}
