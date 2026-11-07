using System.Text.Json;
using ExileSight.Core.Settings;
using ExileSight.Core.Updates;
using Xunit;

namespace ExileSight.Core.Tests.Updates;

public sealed class UpdateChannelTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-channel-" + Guid.NewGuid());

    [Theory]
    [InlineData("stable", "stable", false)]
    [InlineData("prerelease", "beta", true)]
    [InlineData("PRERELEASE", "beta", true)]
    [InlineData("beta", "stable", false)]
    [InlineData("unknown", "stable", false)]
    [InlineData(null, "stable", false)]
    public void SettingsSnapshotSelectsSourceChannelWithoutWriting(string? stored, string channel, bool prerelease)
    {
        var directory = Path.Combine(root, "poe1");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "default.json");
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, game = 1,
            general = new { checkForUpdates = true, updateChannel = stored },
            unrelated = new { keep = "user data" }
        });
        File.WriteAllText(path, json);
        var settings = UpdateSettingsSnapshot.Read(new AppPaths(root)).Settings;
        Assert.Equal(channel, UpdatePolicy.ReleaseChannel(settings));
        Assert.Equal(prerelease, UpdatePolicy.IncludePrereleases(settings));
        Assert.Equal(json, File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void MissingSettingsSelectStableWithoutCreatingFiles()
    {
        var settings = UpdateSettingsSnapshot.Read(new AppPaths(root)).Settings;
        Assert.Equal("stable", UpdatePolicy.ReleaseChannel(settings));
        Assert.False(UpdatePolicy.IncludePrereleases(settings));
        Assert.False(Directory.Exists(root));
        Assert.Equal("https://github.com/DaAwesomePwner/ExileSight", UpdatePolicy.RepositoryUrl);
    }

    [Theory]
    [InlineData("nightly")]
    [InlineData("beta")]
    [InlineData(null)]
    public void UnknownDirectSettingsAlsoSelectStable(string? value)
    {
        var settings = new GeneralSettings { UpdateChannel = value! };
        Assert.Equal("stable", UpdatePolicy.ReleaseChannel(settings));
        Assert.False(UpdatePolicy.IncludePrereleases(settings));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
