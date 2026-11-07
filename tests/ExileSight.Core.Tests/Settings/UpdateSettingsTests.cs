using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class UpdateSettingsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-updates-" + Guid.NewGuid());
    private JsonSettingsStore<SettingsDocument> Open() => new(new AppPaths(root),
        Path.Combine(root, "settings.json"), SettingsJsonContext.Default.SettingsDocument, () => new());

    [Fact]
    public void OldDocumentsDefaultToStableAutomaticChecksWithoutSchemaChange()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "settings.json"), """{"schemaVersion":1,"general":{"language":"en"}}""");
        using var store = Open();
        Assert.True(store.Snapshot.General.CheckForUpdates);
        Assert.Equal("stable", store.Snapshot.General.UpdateChannel);
        Assert.Equal(1, store.Snapshot.SchemaVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nightly")]

    public void UnknownChannelFallsBackWithWarning(string? channel)
    {
        using var store = Open();
        store.Update("general", s => s.General.UpdateChannel = channel!);
        Assert.Equal("stable", store.Snapshot.General.UpdateChannel);
        Assert.Contains(store.Warnings, w => w.Contains("channel"));
    }

    [Theory]
    [InlineData("Stable", "stable")]
    [InlineData("PRERELEASE", "prerelease")]
    public void ChannelIsNormalized(string input, string expected)
    {
        using var store = Open();
        store.Update("general", s => s.General.UpdateChannel = input);
        Assert.Equal(expected, store.Snapshot.General.UpdateChannel);
        Assert.Empty(store.Warnings);
    }

    [Fact]
    public void UnknownPersistedChannelDoesNotQuarantineProfile()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "settings.json");
        File.WriteAllText(path, """{"schemaVersion":1,"general":{"updateChannel":"nightly","language":"fr"}}""");
        var before = File.ReadAllText(path);
        using (var store = Open())
        {
            Assert.Equal("stable", store.Snapshot.General.UpdateChannel);
            Assert.Equal("fr", store.Snapshot.General.Language);
            Assert.Contains(store.Warnings, w => w.Contains("channel"));
        }
        Assert.Equal(before, File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(root));
    }

    [Fact]
    public void PreferencesRoundTrip()
    {
        using (var store = Open())
            store.Update("general", s => { s.General.CheckForUpdates = false; s.General.UpdateChannel = "prerelease"; });
        using var loaded = Open();
        Assert.False(loaded.Snapshot.General.CheckForUpdates);
        Assert.Equal("prerelease", loaded.Snapshot.General.UpdateChannel);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
