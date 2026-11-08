using System.Net;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Pricing;

public sealed class LeagueConcurrencyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ShopWaitsForDiscoveryAlreadyStartedByAnotherConsumer()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>();
        var handler = new PendingHandler(response.Task);
        using var transport = new PriceTransport("1.0", () => handler);
        var service = new PriceService(new AppPaths(root), new PricingTests.Clock(), transport);
        var omni = service.GetLeaguesAsync();
        var shop = service.GetLeaguesAsync();
        var configured = service.GetLeaguesAsync("Hardcore");
        Assert.False(shop.IsCompleted);
        Assert.Equal(1, handler.Calls);
        response.SetResult(new(HttpStatusCode.OK) { Content = new StringContent(PricingTests.Fixture("leagues")) });
        Assert.Equal("Example Challenge", (await omni).DefaultLeague);
        Assert.Equal("Example Challenge", (await shop).DefaultLeague);
        Assert.False((await shop).IsStale);
        Assert.Equal("Hardcore", (await configured).DefaultLeague);
        await service.GetLeaguesAsync();
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CancellingFirstCallerDoesNotCancelSharedDiscovery()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>();
        var handler = new PendingHandler(response.Task);
        using var transport = new PriceTransport("1.0", () => handler);
        var service = new PriceService(new AppPaths(root), new PricingTests.Clock(), transport);
        using var firstCancellation = new CancellationTokenSource();
        var first = service.GetLeaguesAsync(cancellationToken: firstCancellation.Token);
        var second = service.GetLeaguesAsync();
        firstCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(second.IsCompleted);
        response.SetResult(new(HttpStatusCode.OK) { Content = new StringContent(PricingTests.Fixture("leagues")) });
        Assert.Equal("Example Challenge", (await second).DefaultLeague);
        Assert.False((await second).IsStale);
        Assert.Equal(1, handler.Calls);
    }

    private sealed class PendingHandler(Task<HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return response.WaitAsync(cancellationToken);
        }
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
