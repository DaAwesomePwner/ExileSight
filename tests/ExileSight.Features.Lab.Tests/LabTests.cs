using System.Text;
using ExileSight.Core.Logs;
using ExileSight.Features.Lab;
using Xunit;

namespace ExileSight.Features.Lab.Tests;

public sealed class LabTests
{
    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
    [Fact] public void ParsesStringsAndIgnoresUnknownFields()
    {
        var layout = LabImport.Parse(Fixture("valid.json"));
        Assert.Equal(4, layout.Rooms.Count); Assert.Equal(2, layout.Rooms[2].Y);
        Assert.Equal("Fonts", layout.Phase1); Assert.Empty(new LabFeature().Manifest.NetworkHosts);
        Assert.Contains("www.poelab.com", new LabFeature().Manifest.BrowserHosts.ToArray());
    }
    [Theory] [InlineData("missing.json")] [InlineData("dangling.json")]
    public void InvalidFixtureIsRejected(string file) => Assert.Throws<ArgumentException>(() => LabImport.Parse(Fixture(file)));
    [Fact] public void OversizedInputIsRejected() => Assert.Throws<ArgumentException>(() => LabImport.Parse(new byte[LabImport.MaxBytes + 1]));
    [Fact] public void InvalidImportPreservesMapAndTracking()
    {
        var model = new LabModel(); model.Import(Fixture("valid.json"));
        model.Accept(new AreaEntered("", null, "Sanitorium"));
        model.Accept(new AreaEntered("", null, "Halls"));
        var before = model.Snapshot();
        Assert.Throws<ArgumentException>(() => model.Import(Fixture("missing.json")));
        Assert.Equal(before, model.Snapshot());
    }
    [Fact] public void StoreRoundTripAndSeparateDifficulties()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LabStore(directory); store.Save(Fixture("valid.json"));
            store.Save(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Fixture("valid.json")).Replace("Normal", "Uber")));
            Assert.Equal("Normal", store.Load("Normal")!.Difficulty); Assert.Equal("Uber", store.Load("Uber")!.Difficulty);
            Assert.Throws<ArgumentException>(() => store.Save(Fixture("dangling.json")));
            Assert.Equal(4, store.Load("Normal")!.Rooms.Count); Assert.Null(store.Load("Cruel"));
        }
        finally { Directory.Delete(directory, true); }
    }
    [Theory]
    [InlineData("2026-11-08T23:59:59Z", true)] [InlineData("2026-11-09T00:00:00Z", false)]
    [InlineData("2026-11-09T23:59:59Z", false)] [InlineData("2026-11-10T00:00:00Z", true)]
    [InlineData("2026-11-09T19:00:00-06:00", true)]
    public void UsesUtcReset(string now, bool expected) => Assert.Equal(expected, LabImport.Parse(Fixture("valid.json")).IsOutdated(DateTimeOffset.Parse(now)));
    [Fact] public void ReplaysDuplicateNamesUnknownAndDeparture()
    {
        var model = new LabModel(); model.Import(Fixture("valid.json"));
        var parser = new LogParser();
        var lines = Encoding.UTF8.GetString(Fixture("replay.txt")).ReplaceLineEndings("\n").Split('\n');
        foreach (var (line, expected) in lines.Zip(new string?[] { null, "a", "b", "c", "c", null, null }))
        { model.Accept(parser.Parse(line)); Assert.Equal(expected, model.Snapshot().RoomId); }
        Assert.Equal("Not tracking", model.Snapshot().Tracking);
    }
    [Fact] public void AmbiguousAndNonAdjacentLabNamesKeepPosition()
    {
        var layout = LabImport.Parse(Fixture("valid.json"));
        var fork = layout.Rooms[1] with { Exits = new Dictionary<string, string> { ["N"] = "a", ["S"] = "c" } };
        var tracker = new LabTracker(layout with { Rooms = [layout.Rooms[0], fork, layout.Rooms[2], layout.Rooms[3]] });
        tracker.Enter("Halls"); Assert.Null(tracker.Current);
        tracker.Enter("Sanitorium"); Assert.Equal("a", tracker.Current!.Id);
        tracker.Enter("Halls"); tracker.Enter("Sanitorium"); Assert.Equal("b", tracker.Current!.Id); Assert.True(tracker.Uncertain);
        tracker.Enter("Aspirant’s Trial"); Assert.Equal("b", tracker.Current!.Id); Assert.True(tracker.Uncertain);
    }
}
