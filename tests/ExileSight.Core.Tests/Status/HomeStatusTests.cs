using ExileSight.Core.Status;
using Xunit;
namespace ExileSight.Core.Tests.Status;

public sealed class HomeStatusTests
{
    [Fact]
    public void TilesDistinguishHealthyAndProblemStates()
    {
        Assert.True(HomeStatus.Game(true, false, false).Ok);
        Assert.False(HomeStatus.Game(false, false, false).Ok);
        Assert.False(HomeStatus.Game(true, true, false).Ok);
        Assert.True(HomeStatus.Log("Following new entries").Ok);
        Assert.False(HomeStatus.Log("Waiting for setup").Ok);
        Assert.True(HomeStatus.Prices(true, false, "cached").Ok);
        Assert.False(HomeStatus.Prices(true, true, "stale").Ok);
        Assert.False(HomeStatus.Prices(false, false, "missing").Ok);
        Assert.True(HomeStatus.Packs("Packs ready").Ok);
        Assert.False(HomeStatus.Packs("Loading").Ok);
        Assert.Equal("Ready to play", HomeStatus.Title([HomeStatus.Packs("Packs ready")]));
        Assert.Equal("Game not found", HomeStatus.Title([HomeStatus.Game(false, false, false), HomeStatus.Packs("Loading")]));
        Assert.Equal("custom-id", TriggerNames.Display("custom-id"));
    }
    [Fact]
    public void SessionSplitsOnlyBeyondAnHourAndCountsSalesWithinInclusiveWindow()
    {
        var t = new DateTimeOffset(2025, 2, 1, 12, 0, 0, TimeSpan.Zero);
        SessionMap Run(int minute, int? deaths) => new(t.AddMinutes(minute), t.AddMinutes(minute + 5), 300, deaths);
        var result = LastSession.Map([Run(-66, 9), Run(0, 1), Run(65, 2)],
            [new(t, 2, "divine"), new(t.AddMinutes(70), 3, "divine"), new(t.AddSeconds(-1), 100, "divine"),
             new(t.AddMinutes(71), 100, "divine"), new(t, 100, "chaos"), new(t, 100, "divine", true), new(null, 100, "divine")])!;
        Assert.Equal(2, result.Maps.Count);
        Assert.Equal(300, result.AverageSeconds);
        Assert.Equal(3, result.KnownDeaths);
        Assert.True(result.DeathsKnown);
        Assert.Equal(5, result.DivineSold);
        Assert.False(LastSession.Map([Run(0, null)], [])!.DeathsKnown);
        Assert.Null(LastSession.Map([], []));
    }
}
