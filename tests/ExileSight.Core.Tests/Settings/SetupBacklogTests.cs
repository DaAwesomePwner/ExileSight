using System.Text.Json;
using ExileSight.Core.Games;
using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class SetupBacklogTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-setup-" + Guid.NewGuid());
    private AppPaths Paths => new(root);
    private string Seed(string json)
    {
        var dir = Paths.GetSettingsDirectory("poe1");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "default.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"game\":2")]
    [InlineData(",\"game\":null")]
    public void FutureSchemaPrecedesGameValidation(string game)
    {
        var bytes = "{\"schemaVersion\":99" + game + "}";
        var path = Seed(bytes);
        using var store = new JsonSettingsStore<SettingsDocument>(Paths, path, SettingsJsonContext.Default.SettingsDocument,
            () => new() { Game = Game.PoE1 }, expectedGame: Game.PoE1);
        Assert.True(store.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => store.Update("general", _ => { }));
        Assert.Equal(bytes, File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.corrupt-*"));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":99,\"game\":1}")]
    [InlineData("{\"schemaVersion\":1,\"game\":2}")]
    [InlineData("{\"schemaVersion\":0,\"game\":1}")]
    [InlineData("{\"schemaVersion\":1}")]
    public void UpdaterRejectsUnsupportedHeadersWithoutMutation(string json)
    {
        var path = Seed(json);
        Assert.Throws<JsonException>(() => UpdateSettingsSnapshot.Read(Paths));
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void RenameUnknownThenReuseIdForFeatureCommand()
    {
        var path = Seed("""{"schemaVersion":1,"hotkeys":[{"id":"future","key":118,"action":999,"enabled":true}]}""");
        using var store = new JsonSettingsStore<SettingsDocument>(Paths, path, SettingsJsonContext.Default.SettingsDocument, () => new());
        store.Update("hotkeys", s => s.Hotkeys[0].Id = "renamed");
        store.Update("hotkeys", s => s.Hotkeys.Add(new() { Id = "future", Key = 119, FeatureCommand = "guide.next" }));
        store.Flush();
        Assert.False(store.Snapshot.Hotkeys[0].Enabled);
        Assert.True(store.Snapshot.Hotkeys[1].Enabled);
        Assert.Null(store.Snapshot.Hotkeys[1].Action);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.True(json.RootElement.GetProperty("hotkeys")[0].GetProperty("enabled").GetBoolean());
        Assert.Equal("guide.next", store.Snapshot.Hotkeys[1].FeatureCommand);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
