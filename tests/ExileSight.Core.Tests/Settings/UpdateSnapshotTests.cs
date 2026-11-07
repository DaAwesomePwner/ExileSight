using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class UpdateSnapshotTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-readonly-" + Guid.NewGuid());

    [Fact]
    public void MissingSettingsDoNotCreateAnything()
    {
        Assert.True(UpdateSettingsSnapshot.Read(new AppPaths(root)).Settings.CheckForUpdates);
        Assert.False(Directory.Exists(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadingActiveProfileNeverChangesFilesEvenWhenCorrupt(bool corrupt)
    {
        var directory = Path.Combine(root, "poe1");
        Directory.CreateDirectory(directory);
        const string id = "12345678901234567890123456789012";
        File.WriteAllText(Path.Combine(directory, "profiles.json"),
            $$"""{"schemaVersion":1,"game":1,"activeProfileId":"{{id}}"}""");
        File.WriteAllText(Path.Combine(directory, id + ".json"), corrupt ? "broken" :
            """{"schemaVersion":1,"game":1,"general":{"checkForUpdates":false,"updateChannel":"PRERELEASE"}}""");
        File.WriteAllText(Path.Combine(directory, id + ".json.bak"), "backup");
        File.WriteAllText(Path.Combine(directory, id + ".json." + Guid.NewGuid().ToString("N") + ".tmp"), "temporary");
        var before = Directory.GetFiles(directory).ToDictionary(p => p, File.ReadAllText);
        var timestamps = before.Keys.ToDictionary(p => p, File.GetLastWriteTimeUtc);
        if (corrupt)
            Assert.ThrowsAny<System.Text.Json.JsonException>(() => UpdateSettingsSnapshot.Read(new AppPaths(root)));
        else
        {
            var snapshot = UpdateSettingsSnapshot.Read(new AppPaths(root));
            Assert.False(snapshot.Settings.CheckForUpdates);
            Assert.Equal("prerelease", snapshot.Settings.UpdateChannel);
        }
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(directory).Order());
        foreach (var path in before.Keys)
        {
            Assert.Equal(before[path], File.ReadAllText(path));
            Assert.Equal(timestamps[path], File.GetLastWriteTimeUtc(path));
        }
    }

    [Fact]
    public void UnknownChannelWarnsWithoutDiscardingOtherPreferences()
    {
        Directory.CreateDirectory(Path.Combine(root, "poe1"));
        File.WriteAllText(Path.Combine(root, "poe1", "default.json"),
            """{"schemaVersion":1,"game":1,"general":{"checkForUpdates":false,"updateChannel":"nightly"}}""");
        var snapshot = UpdateSettingsSnapshot.Read(new AppPaths(root));
        Assert.False(snapshot.Settings.CheckForUpdates);
        Assert.Equal("stable", snapshot.Settings.UpdateChannel);
        Assert.Single(snapshot.Warnings);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
