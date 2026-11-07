using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Pricing;

public sealed class CacheReviewTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly PricingTests.Clock clock = new();
    private string CachePath
    {
        get
        {
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                PriceEndpoints.Overview("Standard", "Currency").AbsoluteUri.ToUpperInvariant())));
            var directory = Path.Combine(root, "cache/poe1/poeninja");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, key + ".json");
        }
    }
    private PriceService Service(PriceTransport transport) => new(new AppPaths(root), clock, transport);
    private void Seed(PriceCache cache) => File.WriteAllBytes(CachePath,
        JsonSerializer.SerializeToUtf8Bytes(cache, PriceJsonContext.Default.PriceCache));
    private static Task<HttpResponseMessage> Ok() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(PricingTests.Fixture("exchange")) });

    [Fact]
    public async Task FutureTimesAreRewrittenAndExpireAfterOneInterval()
    {
        Seed(new() { LastAttempt = clock.UtcNow.AddDays(5), UpdatedAt = clock.UtcNow.AddDays(5), Json = PricingTests.Fixture("exchange") });
        var calls = 0;
        using var transport = new PriceTransport("1.0", () => new Handler(_ => { calls++; return Ok(); }));
        Assert.Equal(clock.UtcNow, (await Service(transport).GetAsync("Standard", "Currency", TimeSpan.Zero)).UpdatedAt);
        var saved = JsonSerializer.Deserialize(File.ReadAllBytes(CachePath), PriceJsonContext.Default.PriceCache)!;
        Assert.Equal(clock.UtcNow, saved.LastAttempt);
        Assert.Equal(clock.UtcNow, saved.UpdatedAt);
        clock.UtcNow += TimeSpan.FromHours(1);
        await Service(transport).GetAsync("Standard", "Currency", TimeSpan.Zero);
        Assert.Equal(1, calls);
    }
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"LastAttempt\":[]}")]
    [InlineData("oversized")]
    public async Task CorruptRecordsRecoverAfterAtMostOneHour(string content)
    {
        File.WriteAllText(CachePath, content);
        if (content == "oversized")
            using (var stream = File.OpenWrite(CachePath)) stream.SetLength(64 * 1024 * 1024 + 1);
        File.SetLastWriteTimeUtc(CachePath, clock.UtcNow.UtcDateTime);
        var calls = 0;
        using var transport = new PriceTransport("1.0", () => new Handler(_ => { calls++; return Ok(); }));
        Assert.True((await Service(transport).GetAsync("Standard", "Currency", TimeSpan.FromDays(1))).IsStale);
        Assert.Equal(0, calls);
        clock.UtcNow += TimeSpan.FromHours(1);
        Assert.NotNull((await Service(transport).GetAsync("Standard", "Currency", TimeSpan.FromDays(1))).Overview);
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task FailedAttemptUsesOneHourEvenWithLongRefreshInterval()
    {
        var calls = 0;
        using var transport = new PriceTransport("1.0", () => new Handler(_ =>
        {
            calls++;
            if (calls == 1) throw new HttpRequestException();
            return Ok();
        }));
        var service = Service(transport);
        await service.GetAsync("Standard", "Currency", TimeSpan.FromDays(1));
        clock.UtcNow += TimeSpan.FromHours(1);
        Assert.NotNull((await service.GetAsync("STANDARD", "Currency", TimeSpan.FromDays(1))).Overview);
        Assert.Equal(2, calls);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(CachePath)!, "*.json"));
    }
    [Fact]
    public async Task ReadersUseParsedCopyDuringSlowRefreshAndAnotherInstanceLock()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var transport = new PriceTransport("1.0", () => new Handler(_ => ++calls == 1 ? Ok() : pending.Task));
        var service = Service(transport);
        var original = await service.GetAsync("Standard", "Currency", TimeSpan.Zero);
        var again = await service.GetAsync("Standard", "Currency", TimeSpan.Zero);
        Assert.Same(original.Overview, again.Overview);
        clock.UtcNow += TimeSpan.FromHours(1);
        var refreshing = service.GetAsync("Standard", "Currency", TimeSpan.Zero);
        try
        {
            Assert.False(refreshing.IsCompleted);
            var reader = service.GetAsync("standard", "Currency", TimeSpan.Zero);
            Assert.True(reader.IsCompleted);
            Assert.Same(original.Overview, (await reader).Overview);
            Assert.NotNull((await Service(transport).GetAsync("Standard", "Currency", TimeSpan.Zero)).Overview);
        }
        finally
        {
            pending.SetResult(new(HttpStatusCode.OK)
            {
                Content = new StringContent(PricingTests.Fixture("exchange").Replace("\"primaryValue\":10", "\"primaryValue\":20"))
            });
            await refreshing;
        }
        var changed = await service.GetAsync("Standard", "Currency", TimeSpan.Zero);
        Assert.NotSame(original.Overview, changed.Overview);
        Assert.Equal(20m, changed.Overview!.Find("example")!.ChaosValue);
        Assert.Equal(10m, original.Overview!.Find("example")!.ChaosValue);
        using var lease = new FileStream(CachePath + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.NotNull((await Service(transport).GetAsync("Standard", "Currency", TimeSpan.Zero)).Overview);
    }
    internal sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
