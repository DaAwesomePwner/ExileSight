using System.Text.Json;
using Xunit;

namespace ExileSight.Features.Timers.Tests;

public sealed class StoreCompatibilityTests
{
    [Fact]
    public void WritesVersionedDocumentAndReadsLegacyArray()
    {
        using var folder = new TestFolder();
        var row = TimerState.Create("Tea", TimerKind.Countdown, TimeSpan.FromMinutes(1), true);
        var path = Path.Combine(folder.Path, "timers.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new[] { row }));
        var store = new TimerStore(folder.Path); Assert.Equal(row, Assert.Single(store.Snapshot()));
        store.Save(store.Snapshot());
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(row, Assert.Single(new TimerStore(folder.Path).Snapshot()));
    }
    [Theory]
    [InlineData("{\"schemaVersion\":2,\"timers\":[]}")]
    [InlineData("{\"schemaVersion\":2147483648,\"timers\":[]}")]
    [InlineData("{\"schemaVersion\":1.5,\"timers\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"timers\":[],\"future\":true}")]
    public void FutureDocumentsRemainUnchangedAndReadOnly(string json)
    {
        using var folder = new TestFolder(); var path = Path.Combine(folder.Path, "timers.json");
        File.WriteAllText(path, json); var store = new TimerStore(folder.Path);
        Assert.NotNull(store.Notice); Assert.Throws<IOException>(() => store.Save([]));
        Assert.Equal(json, File.ReadAllText(path)); Assert.Empty(Directory.GetFiles(folder.Path, "*.corrupt"));
    }
    [Fact]
    public void UnknownTimerPropertiesRemainReadOnlyWhileKnownDataCanBeDisplayed()
    {
        using var folder = new TestFolder(); var path = Path.Combine(folder.Path, "timers.json");
        var row = TimerState.Create("Tea", TimerKind.Countdown, TimeSpan.FromMinutes(1), true);
        new TimerStore(folder.Path).Save([row]);
        var json = File.ReadAllText(path).Replace("\"name\":", "\"future\":true,\"name\":", StringComparison.Ordinal);
        File.WriteAllText(path, json); var store = new TimerStore(folder.Path);
        Assert.Equal(row, Assert.Single(store.Snapshot())); Assert.NotNull(store.Notice);
        Assert.Throws<IOException>(() => store.Save([])); Assert.Equal(json, File.ReadAllText(path));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TransientReadFailureKeepsOriginalAndBlocksWritesUntilRestart(bool permission)
    {
        using var folder = new TestFolder(); var path = Path.Combine(folder.Path, "timers.json");
        new TimerStore(folder.Path).Save([]); var original = File.ReadAllBytes(path);
        var store = new TimerStore(folder.Path, _ => throw (permission ? new UnauthorizedAccessException() : new IOException()));
        Assert.NotNull(store.Notice); Assert.Throws<IOException>(() => store.Save([]));
        Assert.Equal(original, File.ReadAllBytes(path)); Assert.Empty(Directory.GetFiles(folder.Path, "*.corrupt"));
        Assert.Null(new TimerStore(folder.Path).Notice);
    }
}
