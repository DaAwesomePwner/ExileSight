using ExileSight.Core.Games;
using ExileSight.Core.Settings;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class GameAndBindingTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-game-" + Guid.NewGuid());
    private AppPaths Paths => new(root);
    private string DirectoryPath => Paths.GetSettingsDirectory("poe1");

    private string Seed(string name, string json)
    {
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, name);
        File.WriteAllText(path, json);
        return path;
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"game\":null")]
    [InlineData(",\"game\":2")]
    public void CatalogWithoutMatchingGameRecoversValidatedBackup(string gameField)
    {
        var original = "{\"schemaVersion\":1" + gameField + "}";
        Seed("profiles.json", original);
        Seed("profiles.json.bak", """
            {"schemaVersion":1,"game":1,"profiles":[{"id":"default","name":"Recovered"}]}
            """);
        using var profiles = new ProfileStore(Paths, Game.PoE1);
        Assert.Equal("Recovered", Assert.Single(profiles.Profiles).Name);
        Assert.Contains(profiles.Warnings, x => x.Contains("game"));
        Assert.Equal(original, File.ReadAllText(Assert.Single(Directory.GetFiles(DirectoryPath, "*.corrupt-*"))));
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"game\":null")]
    [InlineData(",\"game\":2")]
    public void ProfileDocumentRequiresGameAndRecoversBackup(string gameField)
    {
        using var profiles = new ProfileStore(Paths, Game.PoE1);
        var original = "{\"schemaVersion\":1" + gameField + "}";
        Seed("default.json", original);
        Seed("default.json.bak", """{"schemaVersion":1,"game":1,"general":{"language":"recovered"}}""");
        using var settings = profiles.Open("default");
        Assert.Equal("recovered", settings.Snapshot.General.Language);
        Assert.Equal(Game.PoE1, settings.Snapshot.Game);
        Assert.Contains(settings.Warnings, x => x.Contains("game"));
        Assert.Equal(original, File.ReadAllText(Assert.Single(Directory.GetFiles(DirectoryPath, "*.corrupt-*"))));
        Assert.Throws<JsonException>(() => settings.Update("game", x => x.Game = null));
    }

    [Fact]
    public void NewProfilesPersistRequestedGame()
    {
        using var profiles = new ProfileStore(Paths, Game.PoE2);
        var id = profiles.Create("New");
        using var settings = profiles.Open(id);
        Assert.Equal(Game.PoE2, settings.Snapshot.Game);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Paths.GetSettingsDirectory("poe2"), id + ".json")));
        Assert.Equal(2, json.RootElement.GetProperty("game").GetInt32());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SnapshotListWriteBackPreservesUnknownBindingState(bool enabled)
    {
        var path = Seed("settings.json", "{\"schemaVersion\":1,\"hotkeys\":[{\"id\":\"future\",\"key\":118," +
            "\"action\":999,\"enabled\":" + (enabled ? "true" : "false") + "}]}");
        using (var store = new JsonSettingsStore<SettingsDocument>(Paths, path,
                   SettingsJsonContext.Default.SettingsDocument, () => new()))
        {
            var snapshot = store.Snapshot;
            Assert.False(snapshot.Hotkeys[0].Enabled);
            snapshot.Hotkeys[0].Id = "FUTURE"; // Identity matching follows Windows casing rules.
            snapshot.Hotkeys[0].Key = 119;
            store.Update("hotkeys", x => x.Hotkeys = snapshot.Hotkeys);
            store.Flush();
            Assert.False(store.Snapshot.Hotkeys[0].Enabled);
        }
        using var saved = JsonDocument.Parse(File.ReadAllText(path));
        var binding = saved.RootElement.GetProperty("hotkeys")[0];
        Assert.Equal(enabled, binding.GetProperty("enabled").GetBoolean());
        Assert.Equal(999, binding.GetProperty("action").GetInt32());
        Assert.Equal(119, binding.GetProperty("key").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MigrationProgrammingFailuresSurfaceWithoutChangingFiles(bool nullReference)
    {
        const string original = """{"schemaVersion":1}""";
        var path = Seed("settings.json", original);
        Seed("settings.json.bak", """{"schemaVersion":2}""");
        var error = Record.Exception(() => new JsonSettingsStore<SettingsDocument>(Paths, path,
            SettingsJsonContext.Default.SettingsDocument, () => new(), new(2, [new BuggyMigration(nullReference)])));
        Assert.IsType(nullReference ? typeof(NullReferenceException) : typeof(InvalidOperationException), error);
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Equal("""{"schemaVersion":2}""", File.ReadAllText(path + ".bak"));
        Assert.Empty(Directory.GetFiles(DirectoryPath, "*.corrupt-*"));
    }

    private sealed class BuggyMigration(bool nullReference) : IMigration
    {
        public int FromVersion => 1;
        public void Apply(JsonObject document)
        {
            document["mutated"] = true;
            if (nullReference) throw new NullReferenceException("Migration bug");
            throw new InvalidOperationException("Migration bug");
        }
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
