using ExileSight.Core.Logs;
using Xunit;
namespace ExileSight.Features.MapTracker.Tests;

public sealed class TransitionTests
{
    internal static LogEnvelope At(int second, long? uptime = null, string message = "") =>
        new(new DateTime(2026, 11, 6).AddSeconds(second), uptime ?? second * 1000, "", "INFO", "Client 42", message);
    internal static ClientLogEvent E(LogEvent value) => new(1, 1, value, false);
    internal static ClientLogEvent Area(int second, string id, long seed = 1) => E(new AreaGenerated("", At(second), 80, id, seed));
    private static RunTracker Start()
    {
        var tracker = new RunTracker();
        tracker.Accept(E(new CharacterLevelUp("", At(0), "Self", "Witch", 90)), "L");
        Assert.True(tracker.ConfirmCharacter("Self"));
        tracker.Accept(Area(0, "MapWorldsBeach"), "L"); return tracker;
    }
    [Theory]
    [InlineData("HideoutCoastal", "Coastal Hideout")]
    [InlineData("1_1_town", "Lioneye's Watch")]
    [InlineData("2_10_town", "Oriath")]
    [InlineData("HeistHub", "The Rogue Harbour")]
    [InlineData("KaruiShores", "Karui Shores")]
    public void GeneratedSafetySurvivesEnteredNameAndCountsReturnPortal(string id, string name)
    {
        var tracker = Start(); tracker.Accept(Area(10, id), "L");
        tracker.Accept(E(new AreaEntered("", At(20), name)), "L");
        tracker.Accept(E(new PlayerSlain("", At(30), "Self")), "L");
        tracker.Accept(Area(100, "MapWorldsBeach"), "L");
        Assert.Equal(10, tracker.Current!.Seconds); Assert.Equal(2, tracker.Current.Portals);
        Assert.Equal(0, tracker.Current.Deaths);
    }
    [Theory]
    [InlineData("HeistArea")]
    [InlineData("Delve")]
    [InlineData("Sanctum")]
    [InlineData("Labyrinth")]
    [InlineData("1_1_1")]
    [InlineData("VaalSideArea")]
    public void DeparturesFromHideoutCannotBecomeSideAreas(string id)
    {
        var tracker = Start(); tracker.Accept(Area(10, "HideoutCoastal"), "L");
        tracker.Accept(Area(20, id), "L");
        tracker.Accept(E(new AreaEntered("", At(30), "Other area")), "L");
        tracker.Accept(E(new PlayerSlain("", At(40), "Self")), "L");
        tracker.Accept(Area(100, "MapWorldsBeach"), "L");
        Assert.Equal(10, tracker.Current!.Seconds); Assert.Equal(0, tracker.Current.Deaths);
        Assert.Equal(2, tracker.Current.Portals);
    }
    [Fact]
    public void VaalAndAbyssStayInRunWithoutConsumingMapPortals()
    {
        var tracker = Start();
        tracker.Accept(Area(10, "VaalSideArea"), "L");
        tracker.Accept(Area(20, "MapWorldsAbyss"), "L");
        tracker.Accept(Area(30, "MapWorldsBeach"), "L");
        Assert.Equal(30, tracker.Current!.Seconds); Assert.Equal(1, tracker.Current.Portals);
    }
    [Fact]
    public void OnlyConfirmedCharacterDeathsCount()
    {
        var tracker = Start();
        tracker.Accept(E(new PlayerSlain("", At(1), "PartyMember")), "L");
        tracker.Accept(E(new PlayerSlain("", At(2), "Self")), "L");
        tracker.ConfirmCharacter(null);
        tracker.Accept(E(new PlayerSlain("", At(3), "Self")), "L");
        Assert.Equal(1, tracker.Current!.Deaths);
        Assert.DoesNotContain("Self", System.Text.Json.JsonSerializer.Serialize(tracker.Current));
    }
    [Theory]
    [InlineData("Abnormal disconnect: An unexpected disconnection occurred.")]
    [InlineData("Connecting to login server at 0.0.0.0:0")]
    [InlineData("Disconnected from server.")]
    public void LogoutAndCharacterSelectCloseBeforeOfflineTime(string message)
    {
        var tracker = Start(); tracker.Accept(E(new Unknown("", At(5))), "L");
        var run = tracker.Accept(E(new Unknown("", At(10, message: message))), "L");
        Assert.True(run!.Incomplete); Assert.Equal(5, run.Seconds);
        tracker.Accept(E(new Unknown("", At(1000))), "L"); Assert.Null(tracker.Current);
        tracker.Accept(Area(1100, "MapWorldsBeach"), "L"); Assert.Equal(0, tracker.Current!.Seconds);
    }
    [Fact]
    public void ClientProcessChangeClosesRunEvenWhenSystemUptimeKeepsIncreasing()
    {
        var tracker = Start(); tracker.Accept(E(new Unknown("", At(30))), "L");
        var old = tracker.Accept(E(new AreaGenerated("", At(100) with { Source = "Client 99" }, 80, "MapWorldsBeach", 1)), "L");
        Assert.True(old!.Incomplete); Assert.Equal(30, old.Seconds);
        Assert.NotEqual(old.Id, tracker.Current!.Id); Assert.Equal(0, tracker.Current.Seconds);
        Assert.False(tracker.CharacterConfirmed);
    }
    [Fact]
    public void ClientRestartClosesOldRunAndStartsFreshWithoutIdentity()
    {
        var tracker = Start(); tracker.Accept(E(new Unknown("", At(30))), "L");
        var old = tracker.Accept(E(new AreaGenerated("", At(100, 1000), 80, "MapWorldsBeach", 1)), "L");
        Assert.True(old!.Incomplete); Assert.Equal(30, old.Seconds); Assert.NotEqual(old.Id, tracker.Current!.Id);
        tracker.Accept(E(new PlayerSlain("", At(101, 2000), "Self")), "L");
        Assert.Equal(0, tracker.Current.Deaths); Assert.Equal(1, tracker.Current.Seconds);
    }
}
