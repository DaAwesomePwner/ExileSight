using ExileSight.Core.Settings;
using ExileSight.Core.Updates;
using Xunit;

namespace ExileSight.Core.Tests.Updates;

public sealed class UpdatePolicyTests
{
    [Fact]
    public void ChecksWaitForStartupAndSixHoursBetweenAttempts()
    {
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        Assert.False(UpdatePolicy.IsDue(new(), start, null, start.AddSeconds(4)));
        Assert.True(UpdatePolicy.IsDue(new(), start, null, start.AddSeconds(5)));
        var last = start.AddSeconds(5);
        Assert.False(UpdatePolicy.IsDue(new(), start, last, last.AddHours(6).AddTicks(-1)));
        Assert.True(UpdatePolicy.IsDue(new(), start, last, last.AddHours(6)));
        Assert.False(UpdatePolicy.IsDue(new() { CheckForUpdates = false }, start, null, start.AddDays(1)));
    }

    [Theory]
    [InlineData("stable", false)]
    [InlineData("prerelease", true)]
    public void ChannelControlsPrereleases(string channel, bool expected) =>
        Assert.Equal(expected, UpdatePolicy.IncludePrereleases(new() { UpdateChannel = channel }));

    [Fact]
    public void UninstalledAppCannotCheckEvenWhenEnabled() =>
        Assert.Equal("not installed via Velopack", UpdatePolicy.UnavailableReason(new(), false));

    [Fact]
    public void DisabledAppCannotCheck() =>
        Assert.Equal("update checks disabled", UpdatePolicy.UnavailableReason(new() { CheckForUpdates = false }, true));

    [Fact]
    public void InstalledEnabledAppCanCheck() => Assert.Null(UpdatePolicy.UnavailableReason(new(), true));
}
