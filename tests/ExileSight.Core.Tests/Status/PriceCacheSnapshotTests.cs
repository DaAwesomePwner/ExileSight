using System.Net;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using ExileSight.Core.Tests.Pricing;
using Xunit;
namespace ExileSight.Core.Tests.Status;

public sealed class PriceCacheSnapshotTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "shell-cache-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task ReadsTheRealWritersCacheWithoutNetworkOrMutationAndDetectsChanges()
    {
        var clock = new PricingTests.Clock();
        var calls = 0;
        using var transport = new PriceTransport("1.0", () => new CacheReviewTests.Handler(_ =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PricingTests.Fixture("exchange")) });
        }));
        var reader = new PriceCacheSnapshotReader(root);
        Assert.False(reader.Read("Standard", "Currency").Available);
        Assert.False(Directory.Exists(root));
        var service = new PriceService(new AppPaths(root), clock, transport);
        await service.GetAsync("Standard", "Currency", TimeSpan.FromHours(1));
        var file = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Single();
        var bytes = File.ReadAllBytes(file);
        var stamp = File.GetLastWriteTimeUtc(file);
        var first = reader.Read("Standard", "Currency");
        Assert.True(first.Available);
        Assert.Equal(clock.UtcNow, first.UpdatedAt);
        Assert.Same(first, reader.Read("Standard", "Currency"));
        Assert.Equal(bytes, File.ReadAllBytes(file));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(file));
        Assert.Equal(1, calls);
        // Timestamp alone invalidates the parsed snapshot, even with identical size/content.
        File.SetLastWriteTimeUtc(file, stamp.AddMinutes(1));
        var second = reader.Read("Standard", "Currency");
        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        // Size alone also invalidates it. Corrupt files are reported, never repaired/quarantined.
        File.WriteAllText(file, "[]"); File.SetLastWriteTimeUtc(file, stamp.AddMinutes(1));
        Assert.False(reader.Read("Standard", "Currency").Available);
        Assert.Equal("[]", File.ReadAllText(file));
        Assert.Single(Directory.GetFiles(root, "*.json", SearchOption.AllDirectories));
        File.Delete(file);
        Assert.False(reader.Read("Standard", "Currency").Available);
        Assert.Equal(1, calls);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
