using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Pricing;

public sealed class CachePolishTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly PricingTests.Clock clock = new();
    private static readonly Uri Uri = PriceEndpoints.Overview("Standard", "Currency");
    private string CachePath
    {
        get
        {
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Uri.AbsoluteUri.ToUpperInvariant())));
            var directory = Path.Combine(root, "cache/poe1/poeninja");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, key + ".json");
        }
    }
    private byte[] Seed()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new PriceCache
        {
            LastAttempt = clock.UtcNow, UpdatedAt = clock.UtcNow, Json = PricingTests.Fixture("exchange")
        }, PriceJsonContext.Default.PriceCache);
        File.WriteAllBytes(CachePath, bytes);
        return bytes;
    }

    [Fact]
    public async Task TransientUnlockedFailureCannotRepairGoodRecord()
    {
        var bytes = Seed();
        var reads = 0;
        var reader = new Reader(_ =>
        {
            if (Interlocked.Increment(ref reads) == 1) throw new IOException("Sharing violation");
            return bytes;
        });
        using var transport = new PriceTransport("1.0", () => new CacheReviewTests.Handler(_ =>
            throw new InvalidOperationException("Fresh cache must not fetch")));
        var cache = new PriceDocumentCache(new AppPaths(root), clock, transport, reader);
        var result = await cache.GetAsync(Uri, _ => { }, TimeSpan.FromHours(1));
        Assert.NotNull(result.Json);
        Assert.False(result.IsStale);
        Assert.Equal(2, reads);
        Assert.Equal(0, reader.TimestampReads);
        Assert.Equal(bytes, File.ReadAllBytes(CachePath));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnlockedReadFailureOrLateSnapshotCannotDisplaceWriter(bool failRead)
    {
        var old = Seed();
        using var writerEntered = new ManualResetEventSlim();
        using var releaseWriter = new ManualResetEventSlim();
        using var readerEntered = new ManualResetEventSlim();
        using var releaseReader = new ManualResetEventSlim();
        var reads = 0;
        var reader = new Reader(_ =>
        {
            var call = Interlocked.Increment(ref reads);
            if (call == 1)
            {
                writerEntered.Set();
                Assert.True(releaseWriter.Wait(TimeSpan.FromSeconds(10)));
                // The writer now observes a newer record than the concurrent unlocked read.
                return Newer();
            }
            if (call == 2)
            {
                readerEntered.Set();
                if (failRead) throw new IOException("Sharing violation");
                Assert.True(releaseReader.Wait(TimeSpan.FromSeconds(10)));
                return old;
            }
            return Newer();
        });
        byte[] Newer() => JsonSerializer.SerializeToUtf8Bytes(new PriceCache
        {
            LastAttempt = clock.UtcNow, UpdatedAt = clock.UtcNow,
            Json = PricingTests.Fixture("exchange").Replace("\"primaryValue\":10", "\"primaryValue\":20")
        }, PriceJsonContext.Default.PriceCache);
        using var transport = new PriceTransport("1.0", () => new CacheReviewTests.Handler(_ =>
            throw new InvalidOperationException("Fresh cache must not fetch")));
        var cache = new PriceDocumentCache(new AppPaths(root), clock, transport, reader);
        var writer = Task.Run(() => cache.GetAsync(Uri, _ => { }, TimeSpan.FromHours(1)));
        Task<CachedPriceDocument>? concurrent = null;
        try
        {
            Assert.True(writerEntered.Wait(TimeSpan.FromSeconds(10)));
            concurrent = Task.Run(() => cache.GetAsync(Uri, _ => { }, TimeSpan.FromHours(1)));
            Assert.True(readerEntered.Wait(TimeSpan.FromSeconds(10)));
            if (failRead)
            {
                var unavailable = await concurrent.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(unavailable.IsStale);
                Assert.Null(unavailable.Json);
                Assert.NotNull(unavailable.Diagnostic);
                // The fake timestamp operation also throws; an unlocked read must never call it.
                Assert.Equal(0, reader.TimestampReads);
            }
            releaseWriter.Set();
            var published = await writer.WaitAsync(TimeSpan.FromSeconds(10));
            releaseReader.Set();
            var concurrentResult = await concurrent.WaitAsync(TimeSpan.FromSeconds(10));
            if (!failRead) Assert.Equal(published.Json, concurrentResult.Json);
            var next = await cache.GetAsync(Uri, _ => { }, TimeSpan.FromHours(1));
            Assert.Equal(published.Json, next.Json);
            Assert.Equal(20m, PriceParser.Parse(next.Json!, "Currency").Find("example")!.ChaosValue);
            Assert.Equal(0, reader.TimestampReads);
        }
        finally
        {
            releaseWriter.Set();
            releaseReader.Set();
            await writer;
            if (concurrent is not null) await concurrent;
        }
    }

    private sealed class Reader(Func<string, byte[]> read) : IPriceCacheReader
    {
        public int TimestampReads;
        public byte[] Read(string path) => read(path);
        public DateTimeOffset LastWriteTime(string path)
        {
            Interlocked.Increment(ref TimestampReads);
            throw new IOException("Timestamp unavailable");
        }
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
