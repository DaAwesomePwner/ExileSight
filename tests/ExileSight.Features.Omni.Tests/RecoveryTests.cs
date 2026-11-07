using ExileSight.App.Features;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Core.Pricing;
using Xunit;

namespace ExileSight.Features.Omni.Tests;

public sealed class RecoveryTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void DispatcherRecoveryRequiresRunningApplication(bool started, bool stopping, bool handled) =>
        Assert.Equal(handled, OverlayFailureGuard.CanHandleDispatcherException(started, stopping));

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void InvalidConfiguredLeagueIsReportedWithoutItsValue(string league)
    {
        var errors = new List<string>();
        var menu = new OmniMenu { League = () => league, ReportFailure = errors.Add };
        menu.Providers = () => [new Contribution()];
        OmniMenuPresentation? shown = null;
        menu.Presented += presentation => shown = presentation;
        menu.Show(ItemTextParser.Parse("Item Class: Rings\nRarity: Normal\nIron Ring", Game.PoE1).Item!);
        Assert.Equal(["ArgumentException"], errors);
        Assert.NotNull(shown);
        Assert.Equal(["PoE Wiki", "PoEDB", "Craft of Exile"], shown.Links.Select(link => link.Label));
        Assert.Equal("Set as equipped", Assert.Single(shown.Entries).Label);
    }
    private sealed class Contribution : IItemMenuProvider
    {
        public IReadOnlyList<ItemMenuEntry> BuildMenu(ParsedItem item) => [new("Set as equipped", () => { })];
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DiscoveryRetriesFailuresOnDemandAfterTenMinutes(bool throws)
    {
        long now = 0;
        var calls = 0;
        var errors = new List<string>();
        var discovery = new OmniLeagueDiscovery(() =>
        {
            calls++;
            if (calls == 1)
            {
                if (throws) throw new IOException("private path");
                return Task.FromResult(new LeagueResult([], "Standard", true, "private diagnostic"));
            }
            return Task.FromResult(new LeagueResult(["Challenge"], "Challenge", false, null));
        }, errors.Add, () => now);
        await discovery.RefreshAsync();
        now = OmniLeagueDiscovery.RetryMilliseconds - 1;
        await discovery.RefreshAsync();
        Assert.Equal(1, calls);
        now++;
        await discovery.RefreshAsync();
        Assert.Equal(2, calls);
        Assert.Equal("Challenge", discovery.Result!.DefaultLeague);
        now += OmniLeagueDiscovery.RetryMilliseconds;
        await discovery.RefreshAsync();
        Assert.Equal(2, calls);
        Assert.Equal([throws ? "IOException" : "InvalidOperationException"], errors);
    }
    [Fact]
    public async Task ConcurrentRequestsShareInFlightDiscovery()
    {
        var completion = new TaskCompletionSource<LeagueResult>();
        var calls = 0;
        var discovery = new OmniLeagueDiscovery(() => { calls++; return completion.Task; }, _ => { });
        var first = discovery.RefreshAsync();
        await discovery.RefreshAsync();
        Assert.Equal(1, calls);
        completion.SetResult(new(["Standard"], "Standard", false, null));
        await first;
    }
}
