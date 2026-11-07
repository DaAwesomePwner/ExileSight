using System.Net;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using ExileSight.Core.Items;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Core.Tests.Pricing;

public sealed class RefreshTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task ConditionalRefreshAndConcurrentReaders()
    {
        var clock = new PricingTests.Clock();
        var calls = 0;
        using var transport = new PriceTransport("1.0", () => new Fake(request =>
        {
            calls++;
            if (calls == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PricingTests.Fixture("exchange")) };
                response.Headers.ETag = new("\"test\"", true);
                response.Content.Headers.LastModified = clock.UtcNow;
                return response;
            }
            Assert.Equal("W/\"test\"", request.Headers.IfNoneMatch.Single().ToString());
            Assert.NotNull(request.Headers.IfModifiedSince);
            return new(HttpStatusCode.NotModified);
        }));
        var service = new PriceService(new AppPaths(root), clock, transport);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => service.GetAsync("Standard", "Currency", TimeSpan.Zero)));
        Assert.Equal(1, calls);
        clock.UtcNow += TimeSpan.FromHours(1);
        var refreshed = await service.GetAsync("Standard", "Currency", TimeSpan.Zero);
        Assert.False(refreshed.IsStale);
        Assert.Equal(clock.UtcNow, refreshed.UpdatedAt);
        Assert.Equal(2, calls);
        Assert.NotNull(refreshed.Overview);
    }
    [Fact]
    public async Task LeaguesDefaultToChallengeAndRespectOverride()
    {
        var calls = 0;
        using var transport = new PriceTransport("1.0", () => new Fake(request =>
        {
            calls++;
            Assert.Equal(PriceEndpoints.Leagues, request.RequestUri);
            return new(HttpStatusCode.OK) { Content = new StringContent(PricingTests.Fixture("leagues")) };
        }));
        var service = new PriceService(new AppPaths(root), new PricingTests.Clock(), transport);
        Assert.Equal("Example Challenge", (await service.GetLeaguesAsync()).DefaultLeague);
        Assert.Equal("Standard", (await service.GetLeaguesAsync("Standard")).DefaultLeague);
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task CancellationIsPropagatedAndAttemptIsThrottled()
    {
        var calls = 0;
        using var cts = new CancellationTokenSource();
        using var transport = new PriceTransport("1.0", () => new Fake(_ =>
        {
            calls++;
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }));
        var service = new PriceService(new AppPaths(root), new PricingTests.Clock(), transport);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync("Standard", "Currency", TimeSpan.Zero, cts.Token));
        var restarted = new PriceService(new AppPaths(root), new PricingTests.Clock(), transport);
        var result = await restarted.GetAsync("Standard", "Currency", TimeSpan.Zero);
        Assert.True(result.IsStale);
        Assert.Null(result.Overview);
        Assert.Equal(1, calls);
    }
    [Fact]
    public void ItemEndpointHistoryAndAmbiguousLookup()
    {
        Assert.Contains("/stash/current/item/overview", PriceEndpoints.Overview("Standard", "UniqueWeapon").AbsoluteUri);
        var overview = PriceParser.Parse(PricingTests.Fixture("items"), "UniqueWeapon");
        var item = new ParsedItem { Game = Game.PoE1, Name = "Example Unique", BaseType = "Example Sword", Rarity = ItemRarity.Unique };
        Assert.NotNull(overview.Find(item));
        Assert.Equal(7, overview.Find(item)!.History.Count);
        Assert.Null(overview.Find(new ParsedItem { Game = Game.PoE1, Name = item.Name, Rarity = ItemRarity.Rare }));
        var ambiguous = overview with { Items = overview.Items.Concat(overview.Items).ToArray() };
        Assert.Null(ambiguous.Find(item));
        Assert.Null(new PriceOverview([], null).ToDivine(1));
    }
    [Fact]
    public void NullHistoryFallsBackToLowConfidenceAndDivinePrimaryConvertsCorrectly()
    {
        var overview = PriceParser.Parse("""
            {"core":{"primary":"divine","rates":{"chaos":200}},
             "items":[{"id":"example","name":"Example"}],
             "lines":[{"id":"example","primaryValue":2,"sparkLine":null,
                       "lowConfidenceSparkLine":{"data":[null,1,2,3,4,5,6]}}]}
            """, "Currency");
        Assert.Equal(400m, overview.Find("example")!.ChaosValue);
        Assert.Equal(2m, overview.Find("example")!.DivineValue);
        Assert.Equal(7, overview.Find("example")!.History.Count);
        Assert.Null(overview.Find("example")!.History[0]);
    }
    private sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
