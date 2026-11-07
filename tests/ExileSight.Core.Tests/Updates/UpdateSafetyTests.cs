using ExileSight.Core.Updates;
using Xunit;

namespace ExileSight.Core.Tests.Updates;

public sealed class UpdateSafetyTests
{
    [Theory]
    [InlineData("https://github.com/a", true)]
    [InlineData("https://api.github.com/a", true)]
    [InlineData("https://objects.githubusercontent.com/a", true)]
    [InlineData("https://release-assets.githubusercontent.com/a", true)]
    [InlineData("https://GITHUB.COM:443/a", true)]
    [InlineData("http://github.com/a", false)]
    [InlineData("https://user@github.com/a", false)]
    [InlineData("https://github.com:444/a", false)]
    [InlineData("https://127.0.0.1/a", false)]
    [InlineData("https://[::1]/a", false)]
    [InlineData("https://github.com.evil.tld/a", false)]
    [InlineData("https://evilgithub.com/a", false)]
    [InlineData("https://api.github.com.evil.tld/a", false)]
    [InlineData("https://user@api.github.com/a", false)]
    [InlineData("https://api.github.com:444/a", false)]
    [InlineData("http://api.github.com/a", false)]
    [InlineData("/relative", false)]
    public void HostPolicy(string url, bool expected) =>
        Assert.Equal(expected, UpdateNetworkPolicy.IsAllowed(new Uri(url, UriKind.RelativeOrAbsolute)));

    [Fact]
    public void EveryRedirectHopIsValidated()
    {
        var first = new Uri("https://github.com/releases");
        var second = UpdateNetworkPolicy.Redirect(first, new Uri("https://objects.githubusercontent.com/file"), 1);
        Assert.Equal("objects.githubusercontent.com", second.Host);
        Assert.Equal("https://objects.githubusercontent.com/next",
            UpdateNetworkPolicy.Redirect(second, new Uri("/next", UriKind.Relative), 2).AbsoluteUri);
        Assert.Throws<ArgumentException>(() => UpdateNetworkPolicy.Redirect(second, new Uri("https://evilgithub.com/a"), 3));
        Assert.Throws<ArgumentException>(() => UpdateNetworkPolicy.Redirect(new Uri("https://evil.tld"), first, 1));
        Assert.Throws<ArgumentException>(() => UpdateNetworkPolicy.Redirect(first, first, 11));
    }

    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, true, true, true)]
    public void ApplyRequiresAllConditions(bool requested, bool downloaded, bool enabled, bool sameChannel, bool expected) =>
        Assert.Equal(expected, UpdatePolicy.ShouldApply(requested, downloaded, enabled, "stable", sameChannel ? "stable" : "prerelease"));

    [Theory]
    [InlineData("stable", "stable", "1.0", "1.0", true)]
    [InlineData("stable", "prerelease", "1.0", "1.0", false)]
    [InlineData("stable", "stable", "1.0", null, false)]
    [InlineData("stable", "stable", "1.0", "2.0", false)]
    public void PendingDownloadSurvivesOnlySameChannelAndConfirmedRelease(string previousChannel, string currentChannel,
        string previousVersion, string? checkedVersion, bool expected) =>
        Assert.Equal(expected, UpdatePolicy.KeepDownload(true, previousChannel, currentChannel, previousVersion, checkedVersion));

    [Theory]
    [InlineData(null, "stable", false)]
    [InlineData("stable", "stable", false)]
    [InlineData("stable", "prerelease", true)]
    [InlineData("prerelease", "stable", true)]
    public void ChannelSwitchClearsPending(string? previous, string current, bool expected) =>
        Assert.Equal(expected, UpdatePolicy.ChannelChanged(previous, current));

    [Fact]
    public void SwitchingBackCannotResurrectClearedDownload()
    {
        var pending = UpdatePolicy.KeepDownload(true, "stable", "prerelease", "1.0", "1.0");
        Assert.False(UpdatePolicy.KeepDownload(pending, "stable", "stable", "1.0", "1.0"));
    }
}
