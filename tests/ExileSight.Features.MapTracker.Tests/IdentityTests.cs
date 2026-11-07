using System.Text.Json;
using System.Text.Json.Nodes;
using ExileSight.Core.Logs;
using Xunit;
namespace ExileSight.Features.MapTracker.Tests;

public sealed class IdentityTests
{
    [Fact]
    public void UnconfirmedRunStaysUnknownAfterLateConfirmationInHistoryTotalsAndCsv()
    {
        var tracker = new RunTracker(); tracker.Accept(TransitionTests.Area(0, "MapWorldsBeach"), "L");
        tracker.Accept(TransitionTests.E(new PlayerSlain("", TransitionTests.At(1), "ExactName")), "L");
        tracker.Accept(Level(2, "ExactName"), "L"); Assert.True(tracker.ConfirmCharacter("ExactName"));
        tracker.Accept(TransitionTests.E(new PlayerSlain("", TransitionTests.At(3), "ExactName")), "L");
        var run = tracker.Accept(TransitionTests.Area(4, "MapWorldsDunes"), "L")!;
        Assert.False(run.DeathsKnown); Assert.Equal("unknown", run.DeathsText);
        Assert.Contains("Deaths: unknown", TrackerModel.DeathTotals([run]));
        Assert.Equal("\"\"", TrackerModel.Csv([run]).Split("\r\n")[1].Split(',')[6]);
        var restored = JsonSerializer.Deserialize<MapRun>(JsonSerializer.Serialize(run))!;
        Assert.False(restored.DeathsKnown); Assert.DoesNotContain("ExactName", JsonSerializer.Serialize(run));
        Assert.True(tracker.Current!.DeathsKnown); Assert.Equal("0", tracker.Current.DeathsText);
    }
    [Fact]
    public void SchemaOneWithoutFlagRemainsKnown()
    {
        var json = JsonSerializer.SerializeToNode(TrackerTests.Run() with { Deaths = 2 })!;
        json.AsObject().Remove("DeathsKnown");
        var restored = json.Deserialize<MapRun>()!;
        Assert.True(restored.DeathsKnown); Assert.Equal("2", restored.DeathsText);
        Assert.Equal("2 deaths", TrackerModel.DeathTotals([restored]));
        Assert.Equal("\"2\"", TrackerModel.Csv([restored]).Split("\r\n")[1].Split(',')[6]);
    }
    [Fact]
    public async Task CandidatesAreCapturedBeforeLeagueSelectionAndMustBeConfirmedExactly()
    {
        var store = new TestStore(); var model = new TrackerModel(store);
        model.Accept(Level(0, "ExactName"));
        Assert.Equal(["ExactName"], model.Candidates);
        Assert.Contains("Deaths: not counted — confirm character", model.Status);
        Assert.False(model.ConfirmCharacter("exactname")); Assert.False(model.ConfirmCharacter("Typo"));
        model.SelectLeague("L"); Assert.True(model.ConfirmCharacter("ExactName"));
        Assert.Contains("Deaths: counting for ExactName", model.Status);
        model.Accept(TransitionTests.Area(1, "MapWorldsBeach"));
        model.Accept(TransitionTests.E(new PlayerSlain("", TransitionTests.At(2), "ExactName")));
        await model.FlushAsync();
        Assert.Equal(1, Assert.Single(store.Read("L")).Deaths);
        Assert.DoesNotContain("ExactName", JsonSerializer.Serialize(store.Read("L")));
        model.Accept(TransitionTests.E(new LogSourceChanged())); await model.FlushAsync();
        Assert.Empty(model.Candidates); Assert.False(model.ConfirmCharacter("ExactName"));
        Assert.Contains("Deaths: not counted — confirm character", model.Status);
    }
    [Fact]
    public void CandidatesAreBoundedAndChatAndDeathsCannotCreateThem()
    {
        var tracker = new RunTracker();
        tracker.Accept(TransitionTests.E(new PlayerSlain("", TransitionTests.At(0), "Party")), "L");
        tracker.Accept(TransitionTests.E(new ChatMessage("", TransitionTests.At(0), "Local", "Chat", "")), "L");
        Assert.Empty(tracker.Candidates);
        for (var i = 0; i < 10; i++) tracker.Accept(Level(i, "Candidate" + i), "L");
        Assert.Equal(5, tracker.Candidates.Length); Assert.Equal("Candidate9", tracker.Candidates[0]);
    }
    internal static ClientLogEvent Level(int second, string name) =>
        TransitionTests.E(new CharacterLevelUp("", TransitionTests.At(second), name, "Witch", 90));
}

internal sealed class TestStore : IRunStore
{
    public readonly List<MapRun> Writes = [];
    public Action<MapRun>? BeforeSave { get; set; }
    public IReadOnlyList<MapRun> Read(string league) => Writes.Where(r => r.League == league)
        .GroupBy(r => r.Id).Select(g => g.Last()).ToArray();
    public void Save(MapRun run) { BeforeSave?.Invoke(run); Writes.Add(run); }
}
