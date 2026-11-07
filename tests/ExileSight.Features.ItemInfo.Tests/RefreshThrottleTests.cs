using ExileSight.App.DataPacks;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class RefreshThrottleTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    [Fact]
    public async Task RestartPreservesFailedAttemptAndSetupGate()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var clock = new Clock();
        var calls = 0;
        Task<string> Fail(CancellationToken _) { calls++; throw new IOException("offline"); }
        Task<string> Success(CancellationToken _) { calls++; return Task.FromResult("available"); }
        try
        {
            var throttle = new PackRefreshThrottle(path, clock);
            Assert.Contains("setup", await throttle.RunAsync("poe1/bases", "v1", () => false, Fail, default));
            Assert.False(Directory.Exists(path));
            await Assert.ThrowsAsync<IOException>(() => throttle.RunAsync("poe1/bases", "v1", () => true, Fail, default));
            var restarted = new PackRefreshThrottle(path, clock);
            Assert.Equal("Failed: IOException", await restarted.RunAsync("poe1/bases", "v1", () => true, Success, default));
            Assert.Equal(1, calls);
            clock.Now += TimeSpan.FromHours(1);
            Assert.Equal("available", await restarted.RunAsync("poe1/bases", "v1", () => true, Success, default));
            Assert.Equal(2, calls);
            await restarted.RunAsync("poe1/mods", "v1", () => true, Success, default);
            await restarted.RunAsync("poe1/bases", "v2", () => true, Success, default);
            Assert.Equal(4, calls);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
