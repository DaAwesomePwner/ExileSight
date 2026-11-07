using Xunit;

namespace ExileSight.Features.Timers.Tests;

public sealed class StoreTests
{
    [Fact]
    public void AtomicRoundtripHasNoTempFilesAndSnapshotsAreIsolated()
    {
        using var folder = new TestFolder(); var store = new TimerStore(folder.Path);
        var row = TimerState.Create("Tea", TimerKind.Countdown, TimeSpan.FromMinutes(2), true);
        store.Save([row]); var snapshot = store.Snapshot(); snapshot[0] = row.Rename("Changed");
        Assert.Equal(row, Assert.Single(new TimerStore(folder.Path).Snapshot()));
        Assert.Single(Directory.GetFiles(folder.Path));
    }
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    public void CorruptionIsQuarantinedAndStartupRecovers(string json)
    {
        using var folder = new TestFolder(); File.WriteAllText(Path.Combine(folder.Path, "timers.json"), json);
        var store = new TimerStore(folder.Path);
        Assert.Empty(store.Snapshot()); Assert.NotNull(store.Notice);
        Assert.Single(Directory.GetFiles(folder.Path, "*.corrupt"));
        store.Save([]); Assert.True(File.Exists(Path.Combine(folder.Path, "timers.json")));
    }
    [Fact]
    public void InvalidAndOversizedDataAreRejectedWithoutReplacingGoodFile()
    {
        using var folder = new TestFolder(); var store = new TimerStore(folder.Path);
        var row = TimerState.Create("Watch", TimerKind.Stopwatch, TimeSpan.Zero, false);
        store.Save([row]);
        Assert.Throws<ArgumentException>(() => store.Save([row, row]));
        Assert.Throws<ArgumentException>(() => store.Save([row with { SavedValue = TimeSpan.MinValue }]));
        Assert.Throws<ArgumentException>(() => store.Save(Enumerable.Range(0, 51).Select(_ => row with { Id = Guid.NewGuid() }).ToArray()));
        Assert.Equal(row, Assert.Single(new TimerStore(folder.Path).Snapshot()));
        File.WriteAllText(Path.Combine(folder.Path, "timers.json"), new string('x', TimerStore.MaxBytes + 1));
        Assert.NotNull(new TimerStore(folder.Path).Notice);
    }
    [Fact]
    public void FailedWriteLeavesMemoryAndPreviousDataIntact()
    {
        using var folder = new TestFolder(); var store = new TimerStore(folder.Path);
        var row = TimerState.Create("Watch", TimerKind.Stopwatch, TimeSpan.Zero, false); store.Save([row]);
        Directory.CreateDirectory(Path.Combine(folder.Path, "timers.json.tmp"));
        var error = Record.Exception(() => store.Save([row.Rename("New")]));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal(row, Assert.Single(store.Snapshot()));
        Assert.Equal(row, Assert.Single(new TimerStore(folder.Path).Snapshot()));
    }
    [Fact]
    public void FailedQuarantinePreservesOriginalAndDisablesWrites()
    {
        using var folder = new TestFolder(); var path = Path.Combine(folder.Path, "timers.json");
        Directory.CreateDirectory(path);
        var store = new TimerStore(folder.Path);
        Assert.NotNull(store.Notice); Assert.Throws<IOException>(() => store.Save([]));
        Assert.True(Directory.Exists(path));
    }
}
