using ExileSight.Core.Status;
using Xunit;
namespace ExileSight.Core.Tests.Status;

public sealed class StatusPresentationTests
{
    [Theory]
    [InlineData(false, false, false, "Start Path of Exile to attach overlays.")]
    [InlineData(false, false, true, "Start Path of Exile to attach overlays.")]
    [InlineData(false, true, true, "Start Path of Exile to attach overlays.")]
    [InlineData(true, true, true, "Restore the game to see overlays.")]
    [InlineData(true, true, false, "Restore the game to see overlays.")]
    [InlineData(true, false, true, "In front")]
    [InlineData(true, false, false, "Overlays wait until the game is in front.")]
    public void GameDetailsRespectAvailability(bool found, bool minimized, bool foreground, string expected) =>
        Assert.Equal(expected, HomeStatus.Game(found, minimized, foreground).Detail);

    [Theory]
    [InlineData("Following new entries", false)]
    [InlineData("Waiting for setup", true)]
    [InlineData("Log reader failed: IOException; retrying.", true)]
    public void LogDetailsAndBlocking(string status, bool blocks)
    {
        Assert.Equal(status, HomeStatus.Log(status).Detail);
        Assert.Equal(blocks, HomeStatus.Log(status).Blocking);
    }
    [Theory]
    [InlineData("Packs ready", false)]
    [InlineData("Loading", true)]
    [InlineData("Missing packs", true)]
    public void PackDetailsAndBlocking(string status, bool blocks)
    {
        Assert.Equal(status, HomeStatus.Packs(status).Detail);
        Assert.Equal(blocks, HomeStatus.Packs(status).Blocking);
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void PricesAreAlwaysAdvisoryAndPreserveDetails(bool available, bool stale)
    {
        var prices = HomeStatus.Prices(available, stale, "Saved Currency prices");
        Assert.Equal("Saved Currency prices", prices.Detail);
        Assert.Equal(available && !stale, prices.Ok);
        Assert.False(prices.Blocking);
        Assert.Equal("Ready to play", HomeStatus.Title([prices, HomeStatus.Game(true, false, false)]));
    }
    [Fact]
    public void FirstBlockingProblemWinsAfterAdvisoryPrices()
    {
        var prices = HomeStatus.Prices(true, true, "stale");
        foreach (var problem in new[] { HomeStatus.Game(false, false, false), HomeStatus.Game(true, true, false),
            HomeStatus.Log("unreadable"), HomeStatus.Packs("missing") })
            Assert.Equal(problem.Summary, HomeStatus.Title([prices, problem, HomeStatus.Log("later problem")]));
    }
    [Theory]
    [InlineData(0, "0 deaths")]
    [InlineData(1, "1 death")]
    [InlineData(2, "2 deaths")]
    public void DeathPluralization(int count, string expected) => Assert.Equal(expected, StatusFormat.Deaths(count));
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(252, "4:12")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(90061, "25:01:01")]
    public void MapDuration(double seconds, string expected) => Assert.Equal(expected, StatusFormat.Duration(seconds));
    [Fact]
    public void DateUsesAnUnambiguousInvariantMonthAndYear() =>
        Assert.Equal("5 Mar 2025 10:00", StatusFormat.Date(new(2025, 3, 5, 10, 0, 0, TimeSpan.Zero)));
    [Theory]
    [InlineData(false, false, false, "Standard", "Standard", false)]
    [InlineData(true, false, false, "Standard", "Standard", true)]
    [InlineData(false, true, false, "Standard", "Standard", true)]
    [InlineData(true, true, true, "Standard", "Standard", false)]
    [InlineData(true, true, false, "Unsaved", "Standard", false)]
    [InlineData(true, true, true, "Unsaved", "Standard", false)]
    [InlineData(true, false, false, null, null, true)]
    public void RefreshNeverReplacesFocusedOrUnsavedEditor(bool changed, bool layout, bool focused,
        string? draft, string? saved, bool rebuild) =>
        Assert.Equal(rebuild, ShellRefreshPolicy.ShouldRebuild(changed, layout, focused, draft, saved));
}
