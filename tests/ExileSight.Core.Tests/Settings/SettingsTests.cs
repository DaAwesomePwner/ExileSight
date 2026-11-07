using ExileSight.Core.Games;
using ExileSight.Core.Settings;
using System.Text.Json.Nodes;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class SettingsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-tests-" + Guid.NewGuid());
    private AppPaths Paths => new(root);
    private JsonSettingsStore<SettingsDocument> Open(string name = "test.json", MigrationPipeline? migrations = null) =>
        new(Paths, Path.Combine(Paths.GetSettingsDirectory("poe1"), name), SettingsJsonContext.Default.SettingsDocument,
            () => new(), migrations, TimeSpan.FromHours(1));

    [Fact]
    public void DefaultsAndRoundTripAreDetached()
    {
        Assert.False(Directory.Exists(Paths.RootDirectory));
        using (var store = Open())
        {
            Assert.Equal("en", store.Snapshot.General.Language);
            Assert.Equal(new[] { 0x76, 0x77 }, store.Snapshot.Hotkeys.Select(x => x.Key));
            var snapshot = store.Snapshot;
            snapshot.General.Language = "fr";
            Assert.Equal("en", store.Snapshot.General.Language);
            store.Update("general", x => x.General.ClientTxtPath = "/game/Client.txt");
            store.Flush();
        }
        using var loaded = Open();
        Assert.Equal("/game/Client.txt", loaded.Snapshot.General.ClientTxtPath);
    }

    [Fact]
    public void AtomicSavesKeepPreviousVersionAndNoTemporaryFiles()
    {
        using var store = Open();
        store.Update("general", x => x.General.Language = "fr");
        store.Flush();
        var path = Path.Combine(Paths.GetSettingsDirectory("poe1"), "test.json");
        var previous = File.ReadAllText(path);
        store.Update("general", x => x.General.Language = "de");
        store.Flush();
        Assert.Equal(previous, File.ReadAllText(path + ".bak"));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public void CorruptFileIsPreservedAndDefaultsReturned()
    {
        var dir = Paths.GetSettingsDirectory("poe1");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "test.json"), "broken");
        using var store = Open();
        Assert.Equal("en", store.Snapshot.General.Language);
        Assert.Single(store.Warnings);
        Assert.Equal("broken", File.ReadAllText(Assert.Single(Directory.GetFiles(dir, "*.corrupt-*"))));
    }

    [Fact]
    public void NewerSchemaCannotBeOverwritten()
    {
        var dir = Paths.GetSettingsDirectory("poe1");
        Directory.CreateDirectory(dir);
        const string future = "{\"schemaVersion\":99,\"general\":{\"language\":\"future\"}}";
        File.WriteAllText(Path.Combine(dir, "test.json"), future);
        using (var store = Open())
        {
            Assert.True(store.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => store.Update("general", x => x.General.Language = "en"));
            store.Flush();
        }
        Assert.Equal(future, File.ReadAllText(Path.Combine(dir, "test.json")));
    }

    [Fact]
    public void MigrationRunsInOrderAndRetainsOriginalBackup()
    {
        var dir = Paths.GetSettingsDirectory("poe1");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "test.json"), "{\"schemaVersion\":1}");
        using var store = Open(migrations: new MigrationPipeline(2, [new SampleMigration()]));
        Assert.Equal(2, store.Snapshot.SchemaVersion);
        Assert.Equal("migrated", store.Snapshot.General.Language);
        store.Flush();
        Assert.Contains("\"schemaVersion\":1", File.ReadAllText(Path.Combine(dir, "test.json.bak")));
    }

    [Fact]
    public void ConcurrentUpdatesAndEventsDoNotLoseChanges()
    {
        using var store = Open();
        var events = 0;
        store.Changed += (_, e) => { Assert.Equal("features", e.Section); Interlocked.Increment(ref events); };
        Parallel.For(0, 50, i => store.Update("features", x => x.Features.Enabled[i.ToString()] = true));
        store.Flush();
        using var loaded = Open();
        Assert.Equal(50, loaded.Snapshot.Features.Enabled.Count);
        Assert.Equal(50, events);
    }

    [Fact]
    public void ProfilesSurviveRestartAndAreSeparatedByGame()
    {
        using (var profiles = new ProfileStore(Paths, Game.PoE1))
        {
            var id = profiles.Create("Build");
            profiles.Rename(id, "Mapping");
            profiles.Switch(id);
            Assert.Equal(id, profiles.ActiveProfileId);
        }
        using var restarted = new ProfileStore(Paths, Game.PoE1);
        Assert.Equal("Mapping", restarted.Profiles.Single(x => x.Id == restarted.ActiveProfileId).Name);
        using var other = new ProfileStore(Paths, Game.PoE2);
        Assert.Single(other.Profiles);
        var deleted = restarted.ActiveProfileId;
        restarted.Delete(deleted);
        Assert.DoesNotContain(restarted.Profiles, x => x.Id == deleted);
        Assert.True(File.Exists(Path.Combine(Paths.GetSettingsDirectory("poe1"), deleted + ".json")));
        Assert.Throws<InvalidOperationException>(() => restarted.Delete(restarted.ActiveProfileId));
    }

    private sealed class SampleMigration : IMigration
    {
        public int FromVersion => 1;
        public void Apply(JsonObject document) => document["general"] = new JsonObject { ["language"] = "migrated" };
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
