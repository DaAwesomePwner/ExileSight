using ExileSight.App.DataPacks;
using Xunit;
using System.Text.Json;

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
    [Theory]
    [InlineData("")]
    [InlineData("\0\0")]
    [InlineData("null")]
    public async Task CorruptRecordRecoversAfterOneHour(string contents)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "poe1-bases.v1.refresh.json");
        var clock = new Clock { Now = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero) };
        var calls = 0;
        Task<string> Refresh(CancellationToken _) { calls++; return Task.FromResult("available"); }
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, contents);
            File.SetLastWriteTimeUtc(path, clock.Now.UtcDateTime);
            var throttle = new PackRefreshThrottle(directory, clock);
            Assert.Contains("Invalid", await throttle.RunAsync("poe1/bases", "v1", () => true, Refresh, default));
            clock.Now += TimeSpan.FromMinutes(59);
            // A restart must preserve the deadline derived from the unchanged file timestamp.
            throttle = new PackRefreshThrottle(directory, clock);
            await throttle.RunAsync("poe1/bases", "v1", () => true, Refresh, default);
            Assert.Equal(0, calls);
            clock.Now += TimeSpan.FromMinutes(1);
            Assert.Equal("available", await throttle.RunAsync("poe1/bases", "v1", () => true, Refresh, default));
            var repaired = JsonSerializer.Deserialize<PackRefreshThrottle.Attempt>(await File.ReadAllTextAsync(path));
            Assert.NotNull(repaired);
            Assert.Equal(clock.Now, repaired.LastAttempt);
            Assert.Equal("available", repaired.Result);
            await new PackRefreshThrottle(directory, clock).RunAsync("poe1/bases", "v1", () => true, Refresh, default);
            Assert.Equal(1, calls);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task FutureAttemptIsCappedOnceAndSurvivesRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "poe1-bases.v1.refresh.json");
        var clock = new Clock();
        var corrected = clock.Now;
        var calls = 0;
        Task<string> Refresh(CancellationToken _) { calls++; return Task.FromResult("available"); }
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new PackRefreshThrottle.Attempt(clock.Now.AddYears(1), "future")));
            await new PackRefreshThrottle(directory, clock).RunAsync("poe1/bases", "v1", () => true, Refresh, default);
            Assert.Equal(corrected, JsonSerializer.Deserialize<PackRefreshThrottle.Attempt>(await File.ReadAllTextAsync(path))!.LastAttempt);
            clock.Now += TimeSpan.FromMinutes(59);
            await new PackRefreshThrottle(directory, clock).RunAsync("poe1/bases", "v1", () => true, Refresh, default);
            Assert.Equal(0, calls);
            clock.Now += TimeSpan.FromMinutes(1);
            await new PackRefreshThrottle(directory, clock).RunAsync("poe1/bases", "v1", () => true, Refresh, default);
            Assert.Equal(1, calls);
        }
        finally { Directory.Delete(directory, true); }
    }

}
