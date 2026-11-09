using System.Text;
using ExileSight.Core.Logs;
using Xunit;

namespace ExileSight.Features.Lab.Tests;

public sealed class LabReviewTests
{
    private static byte[] Fixture(string difficulty = "Normal") => Encoding.UTF8.GetBytes(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "valid.json")).Replace("Normal", difficulty));
    [Theory]
    [InlineData("Unlisted Town")]
    [InlineData("Unlisted Map")]
    public void AnyNonLabAreaEndsTracking(string area)
    {
        var tracker = new LabTracker(LabImport.Parse(Fixture()));
        tracker.Enter("Sanitorium"); tracker.Enter("Halls");
        tracker.Enter(area);
        Assert.Null(tracker.Current); Assert.False(tracker.Active); Assert.False(tracker.Uncertain);
    }
    [Fact]
    public void PlazaHasNoHighlightedRoomAndEntranceStartsTracking()
    {
        var model = new LabModel(); model.Import(Fixture());
        model.Accept(new AreaEntered("", null, "Aspirants' Plaza"));
        Assert.Null(model.Snapshot().RoomId);
        Assert.Equal("At Plaza — not yet in a room", model.Snapshot().Tracking);
        model.Accept(new AreaEntered("", null, "Sanitorium"));
        Assert.Equal("a", model.Snapshot().RoomId);
        Assert.Equal("Tracking: Sanitorium", model.Snapshot().Tracking);
    }
    [Fact]
    public void NonAdjacentRoomNameKeepsLastRoomAndMarksUncertain()
    {
        var tracker = new LabTracker(LabImport.Parse(Fixture()));
        tracker.Enter("Sanitorium"); tracker.Enter("Aspirant’s Trial");
        Assert.Equal("a", tracker.Current!.Id); Assert.True(tracker.Uncertain);
    }
    [Fact]
    public void LatestImportSurvivesRestartAndBrowsingDoesNotChangeIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var model = new LabModel(new LabStore(directory));
            model.Import(Fixture()); model.Import(Fixture("Uber")); model.Select("Normal");
            var restored = new LabModel(new LabStore(directory)); restored.RestoreLatest();
            Assert.Equal("Uber", restored.Snapshot().Layout!.Difficulty);
            Assert.Throws<ArgumentException>(() => model.Import([]));
            Assert.Equal("Uber", new LabStore(directory).LatestDifficulty());
            model.Import(Fixture("Cruel"));
            restored = new LabModel(new LabStore(directory)); restored.RestoreLatest();
            Assert.Equal("Cruel", restored.Snapshot().Layout!.Difficulty);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void SwitchingDifficultyPreservesActiveRunAndKeepsReceivingLogs()
    {
        var model = new LabModel(); model.Import(Fixture("Uber")); model.Import(Fixture());
        model.Accept(new AreaEntered("", null, "Sanitorium"));
        model.Select("Uber"); Assert.Null(model.Snapshot().RoomId);
        model.Accept(new AreaEntered("", null, "Halls"));
        model.Select("Normal"); Assert.Equal("b", model.Snapshot().RoomId);
        model.Select("Cruel"); Assert.Null(model.Snapshot().Layout);
        model.Select("Normal"); Assert.Equal("b", model.Snapshot().RoomId);
    }
}
