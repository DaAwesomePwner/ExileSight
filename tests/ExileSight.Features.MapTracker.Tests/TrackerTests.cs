using ExileSight.Core.Features;
using ExileSight.Core.Logs;
using ExileSight.Features.MapTracker;
using Xunit;
namespace ExileSight.Features.MapTracker.Tests;

public sealed class TrackerTests
{
    private static ClientLogEvent Event(LogEvent e, long dropped = 0) => new(1, 1, e, false) { DroppedBefore = dropped };
    private static LogEnvelope At(int seconds) => new(new DateTime(2026, 11, 6).AddSeconds(seconds), seconds * 1000, "", "", "", "");
    [Fact]
    public void SyntheticSessionReplaysWithPausePortalKillsAndMechanic()
    {
        var tracker = new RunTracker(); tracker.ConfirmCharacter("SyntheticExile"); var completed = new List<MapRun>(); var parser = new LogParser();
        tracker.Copied(new CopiedItem("Maps", "Beach", "Beach Map", 16, ["Synthetic mod"]));
        foreach (var line in File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "session.txt")))
            if (tracker.Accept(Event(parser.Parse(line)), "Fixture league") is { } run) completed.Add(run);
        var first = Assert.Single(completed);
        Assert.Equal("Beach", first.Map); Assert.Equal(16, first.Tier); Assert.Equal(20, first.Seconds);
        Assert.Equal(1, first.Deaths); Assert.Equal(2, first.Portals); Assert.Equal(25, first.Kills); Assert.Contains("Blight", first.Mechanics);
        Assert.Equal(["Synthetic mod"], first.Mods); Assert.Empty(tracker.Current!.Mods);
        var last = tracker.Accept(Event(new LogSourceChanged()), "Fixture league");
        Assert.True(last!.Incomplete); Assert.Equal("Dunes", last.Map);
    }
    [Fact]
    public void SideAreaDeathsAndGapNeverExtendAcrossMissingTime()
    {
        var tracker = new RunTracker(); tracker.ConfirmCharacter("Private name");
        tracker.Accept(Event(new AreaGenerated("", At(0), 70, "MapWorldsBeach", 1)), "L");
        tracker.Accept(Event(new AreaGenerated("", At(5), 70, "Incursion", 2)), "L");
        tracker.Accept(Event(new PlayerSlain("secret", At(7), "Private name")), "L");
        tracker.Accept(Event(new NpcDialogue("", At(8), "Sister Cassia", "private")), "L");
        var run = tracker.Accept(Event(new Unknown("", At(100)), 2), "L");
        Assert.True(run!.Incomplete); Assert.Equal(8, run.Seconds); Assert.Equal(1, run.Deaths);
        Assert.Empty(run.Mechanics); Assert.Null(tracker.Current);
    }
    [Fact]
    public void NoKillBaselineMeansUnknownAndResetDoesNotGoNegative()
    {
        var tracker = new RunTracker();
        tracker.Accept(Event(new AreaGenerated("", At(0), 70, "MapWorldsBeach", 1)), "L");
        tracker.Accept(Event(new KillCountReported("", At(1), 100)), "L");
        Assert.Null(tracker.Current!.Kills);
        tracker.Accept(Event(new KillCountReported("", At(2), 5)), "L");
        Assert.Null(tracker.Current.Kills);
    }
    [Fact]
    public void CsvEscapesQuotesNewlinesAndFormulas()
    {
        var csv = TrackerModel.Csv([Run() with { Notes = "=HYPERLINK(\"bad\")\nnext" }]);
        Assert.Contains("\"'=HYPERLINK(\"\"bad\"\")\nnext\"", csv);
    }
    internal static MapRun Run() => new(1, Guid.NewGuid(), "L", "Beach", 1, DateTime.Today,
        DateTime.Today, 3, 1, 0, null, [], [], "", false);
}
