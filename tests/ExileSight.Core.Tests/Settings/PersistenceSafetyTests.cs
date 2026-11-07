using System.Text.Json;
using System.Text.Json.Nodes;
using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class PersistenceSafetyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-safety-" + Guid.NewGuid());
    private AppPaths Paths => new(root);
    private string FilePath => Path.Combine(root, "poe1", "test.json");
    private JsonSettingsStore<SettingsDocument> Open(TimeSpan? delay = null, MigrationPipeline? migrations = null) =>
        new(Paths, FilePath, SettingsJsonContext.Default.SettingsDocument, () => new(), migrations, delay);

    private void Seed(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, json);
    }

    [Fact]
    public void UnknownPropertiesAndFeatureDataSurviveUpdates()
    {
        Seed("""{"schemaVersion":1,"future":7,"general":{"extra":"kept"},"features":{"sections":{"omni":{"size":42}}}}""");
        using (var store = Open())
        {
            store.Update("general", x => x.General.FirstRunCompleted = true);
        }
        using var loaded = Open();
        Assert.Equal(7, loaded.Snapshot.ExtensionData["future"].GetInt32());
        Assert.Equal("kept", loaded.Snapshot.General.ExtensionData["extra"].GetString());
        Assert.Equal(42, loaded.Snapshot.Features.Sections["omni"]["size"].GetInt32());
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"general\":null}")]
    [InlineData("{\"schemaVersion\":1,\"hotkeys\":[null]}")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":\"future\"}")]
    public void InvalidDocumentsAreQuarantinedWithoutLosingBytes(string json)
    {
        Seed(json);
        using var store = Open();
        Assert.Single(store.Warnings);
        Assert.Equal(json, File.ReadAllText(Assert.Single(Directory.GetFiles(Path.GetDirectoryName(FilePath)!, "*.corrupt-*"))));
    }

    [Fact]
    public void FailedMigrationPreservesSourceAndRecoversDefaults()
    {
        const string json = "{\"schemaVersion\":1}";
        Seed(json);
        using var store = Open(migrations: new(2, [new BrokenMigration()]));
        Assert.Equal("en", store.Snapshot.General.Language);
        Assert.Equal(2, store.Snapshot.SchemaVersion);
        Assert.Contains(store.Warnings, x => x.Contains("Migration failed."));
        Assert.Equal(json, File.ReadAllText(Assert.Single(
            Directory.GetFiles(Path.GetDirectoryName(FilePath)!, "*.corrupt-*"))));
    }

    [Fact]
    public void FailedUpdateLeavesSnapshotAndDiskUnchanged()
    {
        using var store = Open();
        Assert.Throws<InvalidOperationException>(() => store.Update("general", x =>
        {
            x.General.Language = "de";
            throw new InvalidOperationException();
        }));
        Assert.Throws<JsonException>(() => store.Update("overlay", x => x.Overlay.ScaleOverride = -1));
        Assert.Equal("en", store.Snapshot.General.Language);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void FailedAtomicReplacePreservesOriginalAndCanRetry()
    {
        using var store = Open(TimeSpan.FromHours(1));
        store.Update("general", x => x.General.Language = "first");
        store.Flush();
        var original = File.ReadAllText(FilePath);
        Directory.CreateDirectory(FilePath + ".bak");
        store.Update("general", x => x.General.Language = "second");
        var error = Record.Exception(() => store.Flush());
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal(original, File.ReadAllText(FilePath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(FilePath)!, "*.tmp"));
        Directory.Delete(FilePath + ".bak");
        store.Flush();
        store.Update("general", x => x.General.Language = "third");
        store.Flush();
        using var backup = JsonDocument.Parse(File.ReadAllText(FilePath + ".bak"));
        Assert.Equal("second", backup.RootElement.GetProperty("general").GetProperty("language").GetString());
    }

    [Fact]
    public async Task DebounceEventuallyPersistsLastUpdate()
    {
        using var store = Open(TimeSpan.FromMilliseconds(30));
        for (var i = 0; i < 30; i++)
            store.Update("general", x => x.General.Language = i.ToString());
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!File.Exists(FilePath) && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(File.Exists(FilePath));
        using var loaded = Open();
        Assert.Equal("29", loaded.Snapshot.General.Language);
    }

    [Fact]
    public void EscapingAndLinkedPathsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Paths.GetGameDirectory("../outside"));
        Assert.Throws<IOException>(() => new JsonSettingsStore<SettingsDocument>(Paths,
            Path.Combine(root, "..", "escaped.json"), SettingsJsonContext.Default.SettingsDocument, () => new()));
        Directory.CreateDirectory(root);
        var outside = Path.Combine(root, "outside");
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(Path.Combine(root, "poe1"), outside);
        Assert.Throws<IOException>(() => Open());
        Assert.Empty(Directory.GetFiles(outside));
        Directory.Delete(Path.Combine(root, "poe1"));
    }

    [Fact]
    public void BackupSymlinkCannotRedirectWrites()
    {
        using var store = Open(TimeSpan.FromHours(1));
        store.Update("general", _ => { });
        store.Flush();
        var target = Path.Combine(root, "untouched");
        File.WriteAllText(target, "safe");
        File.CreateSymbolicLink(FilePath + ".bak", target);
        store.Update("general", x => x.General.Language = "de");
        Assert.Throws<IOException>(() => store.Flush());
        Assert.Equal("safe", File.ReadAllText(target));
        File.Delete(FilePath + ".bak");
        store.Flush();
    }

    private sealed class BrokenMigration : IMigration
    {
        public int FromVersion => 1;
        public void Apply(JsonObject document) => throw new InvalidOperationException("Migration failed.");
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
