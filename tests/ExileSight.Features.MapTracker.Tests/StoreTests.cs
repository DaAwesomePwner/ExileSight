using ExileSight.Features.MapTracker;
using Xunit;
namespace ExileSight.Features.MapTracker.Tests;

public sealed class StoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    [Fact]
    public void RoundTripUpdateIsolationAndCorruptQuarantine()
    {
        var store = new RunStore(directory); var run = TrackerTests.Run(); store.Save(run);
        store.Save(run with { Notes = "note" });
        Assert.Equal("note", Assert.Single(store.Read("L")).Notes); Assert.Empty(store.Read("Other"));
        File.WriteAllText(store.PathFor("L"), "broken"); Assert.Empty(store.Read("L"));
        Assert.True(File.Exists(store.PathFor("L") + ".corrupt"));
    }
    [Fact]
    public void FutureSchemaIsNotOverwritten()
    {
        var store = new RunStore(directory); var run = TrackerTests.Run(); store.Save(run with { Schema = 2 });
        var before = File.ReadAllText(store.PathFor("L"));
        Assert.Throws<NotSupportedException>(() => store.Save(run));
        Assert.Equal(before, File.ReadAllText(store.PathFor("L")));
    }
    [Fact]
    public void RotationIsBounded()
    {
        var store = new RunStore(directory, 1024);
        for (var i = 0; i < 20; i++) store.Save(TrackerTests.Run());
        Assert.True(new FileInfo(store.PathFor("L")).Length <= 1024);
        Assert.True(new FileInfo(store.PathFor("L") + ".previous").Length <= 1024);
    }
}
