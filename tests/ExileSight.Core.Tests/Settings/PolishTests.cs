using ExileSight.Core.Games;
using ExileSight.Core.Settings;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class PolishTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-polish-" + Guid.NewGuid());
    private string Main => Path.Combine(root, "test.json");
    private void Seed(string text, string? path = null)
    {
        path ??= Main;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
    private JsonSettingsStore<SettingsDocument> Open(Action<string, string, string?>? replace = null,
        Action<Stream, byte[]>? write = null, MigrationPipeline? migrations = null) =>
        new(new AppPaths(root), Main, SettingsJsonContext.Default.SettingsDocument, () => new(), migrations,
            TimeSpan.FromHours(1), replace, temporaryWriteOperation: write);

    [Fact]
    public void NullBackupReplacementFailurePromotesCompleteTempNotUntrustedBackup()
    {
        Seed("bad main");
        Seed("bad backup", Main + ".bak");
        var attempts = 0;
        using var store = Open((source, destination, backup) =>
        {
            Assert.Null(backup);
            if (attempts++ == 0)
            {
                File.Delete(destination);
                throw new IOException("partial replacement");
            }
            File.Replace(source, destination, backup);
        });
        store.Flush();
        store.Update("general", x => x.General.Language = "new");
        Assert.Throws<IOException>(() => store.Flush());
        Assert.Contains("new", File.ReadAllText(Main));
        Assert.Equal("bad backup", File.ReadAllText(Main + ".bak"));
        store.Flush();
    }

    [Fact]
    public void FailedWriteCannotPromoteIncompleteTemporaryFile()
    {
        var attempts = 0;
        using var store = Open(write: (stream, bytes) =>
        {
            if (attempts++ == 0)
            {
                stream.Write(bytes.AsSpan(0, 5));
                throw new IOException("partial write");
            }
            stream.Write(bytes);
        });
        store.Update("general", _ => { });
        Assert.Throws<IOException>(() => store.Flush());
        Assert.False(File.Exists(Main));
        Assert.Equal(5, new FileInfo(Assert.Single(Directory.GetFiles(root, "*.tmp"))).Length);
        store.Flush();
    }

    [Fact]
    public void FailedRestoreRetainsTemporaryBytesAndOriginalBackup()
    {
        Seed("""{"schemaVersion":1}""");
        var attempts = 0;
        using var store = Open((source, destination, backup) =>
        {
            if (attempts++ == 0)
            {
                File.Move(destination, backup!);
                Directory.CreateDirectory(destination); // Prevent both copy and move recovery.
                throw new IOException("partial replacement");
            }
            File.Replace(source, destination, backup);
        });
        store.Update("general", x => x.General.Language = "retained");
        Assert.Throws<IOException>(() => store.Flush());
        Assert.Contains("retained", File.ReadAllText(Assert.Single(Directory.GetFiles(root, "*.tmp"))));
        Assert.Equal("""{"schemaVersion":1}""", File.ReadAllText(Main + ".bak"));
        Directory.Delete(Main);
        store.Flush();
    }

    [Fact]
    public void MismatchedCatalogGameRecoversMatchingBackup()
    {
        var path = Path.Combine(root, "poe1", "profiles.json");
        Seed("""{"schemaVersion":1,"game":2}""", path);
        Seed("""{"schemaVersion":1,"game":1,"profiles":[{"id":"default","name":"Recovered"}]}""", path + ".bak");
        using var profiles = new ProfileStore(new AppPaths(root), Game.PoE1);
        Assert.Equal("Recovered", Assert.Single(profiles.Profiles).Name);
        Assert.Contains(profiles.Warnings, x => x.Contains("game", StringComparison.OrdinalIgnoreCase));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.corrupt-*"));
    }

    [Fact]
    public void FailedOlderMigrationRecoversAndMigratesBackup()
    {
        Seed("""{"schemaVersion":1,"general":{"language":"broken"}}""");
        Seed("""{"schemaVersion":1,"general":{"language":"good"}}""", Main + ".bak");
        using var store = Open(migrations: new(2, [new ConditionalMigration()]));
        Assert.Equal("good", store.Snapshot.General.Language);
        Assert.True(store.Snapshot.General.FirstRunCompleted);
        Assert.Equal(2, store.Snapshot.SchemaVersion);
        store.Flush();
        Assert.Contains("good", File.ReadAllText(Main));
        Assert.Contains("broken", File.ReadAllText(Assert.Single(Directory.GetFiles(root, "*.corrupt-*"))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnknownBindingRuntimeDisableDoesNotChangePersistedEnabled(bool enabled)
    {
        Seed("{\"schemaVersion\":1,\"hotkeys\":[{\"id\":\"future\",\"key\":118,\"action\":999,\"enabled\":" +
            (enabled ? "true" : "false") + "}]}");
        using (var store = Open())
        {
            Assert.False(Assert.Single(store.Snapshot.Hotkeys).Enabled);
            Assert.NotEmpty(store.Warnings);
            store.Update("general", x => x.General.Language = "edited");
        }
        using var json = JsonDocument.Parse(File.ReadAllText(Main));
        var binding = json.RootElement.GetProperty("hotkeys")[0];
        Assert.Equal(enabled, binding.GetProperty("enabled").GetBoolean());
        Assert.Equal(999, binding.GetProperty("action").GetInt32());
        using var restarted = Open();
        Assert.False(Assert.Single(restarted.Snapshot.Hotkeys).Enabled);
    }

    private sealed class ConditionalMigration : IMigration
    {
        public int FromVersion => 1;
        public void Apply(JsonObject document)
        {
            if (document["general"]!["language"]!.GetValue<string>() == "broken")
                throw new InvalidOperationException("Invalid older document");
            document["general"]!["firstRunCompleted"] = true;
        }
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
