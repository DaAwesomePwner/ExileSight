using System.Net;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Time;
using Xunit;

namespace ExileSight.Core.Tests.Pricing;

public sealed class PricingTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    internal static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "fixtures"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "tests/fixtures/poe1/poeninja", name + ".json"));
    }
    [Fact]
    public void ParsesExchangeAndLegacy()
    {
        var data = PriceParser.Parse(Fixture("exchange"), "Currency");
        Assert.Equal(200m, data.ChaosPerDivine);
        Assert.Equal(0.05m, data.Find("example")!.DivineValue);
        Assert.Null(data.Find("divine")!.History[0]);
        Assert.Equal(12, data.Find("Example Orb")!.ListingCount);
        Assert.Equal(400m, data.ToChaos(2));
        Assert.Equal(2m, data.ToDivine(400));
        var legacy = PriceParser.Parse(Fixture("legacy"), "UniqueWeapon");
        Assert.Equal("Example Sword", legacy.Find("1")!.BaseType);
        Assert.Equal(40m, legacy.Find("Example Unique")!.ChaosValue);
    }
    [Fact]
    public void UrlEscapesAndHostsAreExact()
    {
        Assert.Contains("league=A+%26+B&type=Currency", PriceEndpoints.Overview("A & B", "Currency").AbsoluteUri);
        foreach (var url in new[] { "http://poe.ninja/", "https://poe.ninja.evil/", "https://user@poe.ninja/", "https://poe.ninja:444/" })
            Assert.False(PriceTransport.IsAllowed(new Uri(url)));
    }
    [Fact]
    public async Task PersistsFreshnessAndFailedAttemptsAcrossRestarts()
    {
        var clock = new Clock();
        var handler = new Handler();
        using var transport = new PriceTransport("1.0", () => handler);
        var service = new PriceService(new AppPaths(root), clock, transport);
        var first = await service.GetAsync("Standard", "Currency", TimeSpan.Zero);
        Assert.False(first.IsStale);
        Assert.Equal(1, handler.Calls);
        service = new PriceService(new AppPaths(root), clock, transport);
        await service.GetAsync("Standard", "Currency", TimeSpan.Zero);
        Assert.Equal(1, handler.Calls);
        clock.UtcNow += TimeSpan.FromHours(1);
        handler.Fail = true;
        Assert.True((await service.GetAsync("Standard", "Currency", TimeSpan.Zero)).IsStale);
        service = new PriceService(new AppPaths(root), clock, transport);
        await service.GetAsync("Standard", "Currency", TimeSpan.Zero);
        Assert.Equal(2, handler.Calls);
    }
    [Fact]
    public async Task RejectsRedirectAndOversizedBody()
    {
        using var redirect = new PriceTransport("1.0", () => new Handler { Redirect = true });
        await Assert.ThrowsAsync<HttpRequestException>(() => redirect.GetAsync(PriceEndpoints.Overview("Standard", "Currency"), null, null, default));
        using var small = new PriceTransport("1.0", () => new Handler(), 4);
        await Assert.ThrowsAsync<HttpRequestException>(() => small.GetAsync(PriceEndpoints.Overview("Standard", "Currency"), null, null, default));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    internal sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public long Milliseconds => 0;
        public void Delay(int milliseconds) => throw new NotSupportedException();
    }
    internal sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        public bool Fail, Redirect;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("ExileSight/1.0", request.Headers.UserAgent.ToString());
            if (Fail) throw new HttpRequestException("offline");
            var response = new HttpResponseMessage(Redirect ? HttpStatusCode.Redirect : HttpStatusCode.OK)
            { Content = new StringContent(Fixture("exchange")) };
            if (Redirect) response.Headers.Location = new Uri("https://example.com/");
            return Task.FromResult(response);
        }
    }
}
