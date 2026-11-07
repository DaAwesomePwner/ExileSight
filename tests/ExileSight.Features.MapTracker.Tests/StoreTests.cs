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
    public void CapRefusesWriteWithoutLosingAnySavedRow()
    {
        var store = new RunStore(directory, 1024);
        var saved = new List<Guid>();
        for (var i = 0; i < 20; i++)
        {
            var run = TrackerTests.Run();
            var before = File.Exists(store.PathFor("L")) ? File.ReadAllText(store.PathFor("L")) : "";
            try { store.Save(run); saved.Add(run.Id); }
            catch (IOException)
            {
                Assert.Equal(before, File.ReadAllText(store.PathFor("L")));
                Assert.Equal(saved, store.Read("L").Select(r => r.Id));
                return;
            }
        }
        Assert.Fail("Expected capacity refusal.");
    }
    [Fact]
    public void UnknownSchemaOneFieldsSurviveReplacementAndOtherWrites()
    {
        var store = new RunStore(directory); var run = TrackerTests.Run(); store.Save(run);
        var path = store.PathFor("L");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        json["FutureField"] = new System.Text.Json.Nodes.JsonObject { ["nested"] = 42 };
        File.WriteAllText(path, json.ToJsonString() + "\n");
        store.Save(run with { Notes = "updated" }); store.Save(TrackerTests.Run());
        Assert.Equal(42, store.Read("L").Single(r => r.Id == run.Id).Extra!["FutureField"].GetProperty("nested").GetInt32());
    }
}
