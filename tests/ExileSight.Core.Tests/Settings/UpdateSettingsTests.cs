using System.Text.Json;
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
    [InlineData("Stable")]
    public void InvalidChannelIsRejected(string? channel)
    {
        using var store = Open();
        Assert.Throws<JsonException>(() => store.Update("general", s => s.General.UpdateChannel = channel!));
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
