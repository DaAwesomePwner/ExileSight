using ExileSight.Core.Games;
using ExileSight.Core.Settings;
using System.Text.Json;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class ReviewRegressionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-review-" + Guid.NewGuid());
    private AppPaths Paths => new(root);
    private string Main => Path.Combine(Paths.RootDirectory, "poe1", "test.json");
    private JsonSettingsStore<SettingsDocument> Open(Action<string, string, string?>? replace = null) =>
        new(Paths, Main, SettingsJsonContext.Default.SettingsDocument, () => new(), replaceOperation: replace);
    private void Seed(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecoveryUsesBackupBeforeDefaultsAndSurvivesTwoSaves(bool corrupt)
    {
        if (corrupt) Seed(Main, "broken");
        Seed(Main + ".bak", """{"schemaVersion":1,"general":{"language":"recovered"}}""");
        using var store = Open();
        Assert.Equal("recovered", store.Snapshot.General.Language);
        store.Update("general", x => x.General.FirstRunCompleted = true);
        store.Flush();
        store.Update("overlay", x => x.Overlay.Enabled = false);
        store.Flush();
        using var backup = JsonDocument.Parse(File.ReadAllText(Main + ".bak"));
        Assert.Equal("recovered", backup.RootElement.GetProperty("general").GetProperty("language").GetString());
    }

    [Fact]
    public void DefaultsDoNotRotateFailedBackup()
    {
        Seed(Main, "bad main");
        Seed(Main + ".bak", "bad backup");
        using var store = Open();
        store.Update("general", x => x.General.Language = "first");
        store.Flush();
        store.Update("general", x => x.General.Language = "second");
        store.Flush();
        Assert.Equal("bad backup", File.ReadAllText(Main + ".bak"));
        Assert.Equal("bad main", File.ReadAllText(Assert.Single(Directory.GetFiles(Path.GetDirectoryName(Main)!, "*.corrupt-*"))));
    }

    [Fact]
    public void PartialReplacementFailureRestoresMainAndSurfacesError()
    {
        Seed(Main, """{"schemaVersion":1,"general":{"language":"original"}}""");
        var calls = 0;
        using var store = Open((temporary, destination, backup) =>
        {
            if (calls++ == 0)
            {
                File.Move(destination, backup!);
                throw new IOException("Simulated Windows partial replace");
            }
            File.Replace(temporary, destination, backup);
        });
        store.Update("general", x => x.General.Language = "new");
        Assert.Throws<IOException>(() => store.Flush());
        Assert.Contains("original", File.ReadAllText(Main));
        Assert.Contains("original", File.ReadAllText(Main + ".bak"));
        store.Flush();
        Assert.Contains("new", File.ReadAllText(Main));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void InvalidGameRejectedAtApiAndOnRead(int game)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileStore(Paths, (Game)game));
        Seed(Main, "{\"schemaVersion\":1,\"game\":" + game + "}");
        using var store = Open();
        Assert.Contains(store.Warnings, x => x.Contains("game", StringComparison.OrdinalIgnoreCase));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(Main)!, "*.corrupt-*"));
        Seed(Path.Combine(Paths.GetSettingsDirectory("poe1"), "profiles.json"),
            "{\"schemaVersion\":1,\"game\":" + game + "}");
        using var profiles = new ProfileStore(Paths, Game.PoE1);
        Assert.Contains(profiles.Warnings, x => x.Contains("game", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FutureCatalogListsReadableProfilesAndRefusesWrites()
    {
        Seed(Path.Combine(Paths.GetSettingsDirectory("poe1"), "profiles.json"),
            """{"schemaVersion":99,"activeProfileId":"default","profiles":[{"id":"default","name":"Existing"}]}""");
        using var store = new ProfileStore(Paths, Game.PoE1);
        Assert.True(store.IsReadOnlyNewerSchema);
        Assert.Equal("Existing", Assert.Single(store.Profiles).Name);
        Assert.Throws<InvalidOperationException>(() => store.Rename("default", "Changed"));
    }

    [Fact]
    public void LinkedRootResolvesAndSupportsSaving()
    {
        var target = root + "-target";
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(root, target);
        try
        {
            using var store = Open();
            store.Update("general", x => x.General.Language = "linked");
            store.Flush();
            Assert.True(File.Exists(Path.Combine(target, "poe1", "test.json")));
        }
        finally { Directory.Delete(root); Directory.Delete(target, true); }
    }

    [Fact]
    public void UnknownActionDisablesOnlyItsBinding()
    {
        Seed(Main, """{"schemaVersion":1,"hotkeys":[{"id":"future","key":118,"action":999}]}""");
        using var store = Open();
        Assert.False(Assert.Single(store.Snapshot.Hotkeys).Enabled);
        Assert.Contains(store.Warnings, x => x.Contains("future"));
        Assert.True(File.Exists(Main));
    }

    [Fact]
    public void DuplicateKeysRecoverAndStaleTempsAreCleaned()
    {
        Seed(Main, """{"schemaVersion":1,"general":{"language":"en","language":"fr"}}""");
        var temp = Main + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Seed(temp, "stale");
        using var store = Open();
        Assert.NotEmpty(store.Warnings);
        Assert.False(File.Exists(temp));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(Main)!, "*.corrupt-*"));
    }

    [Fact]
    public void ProfileIdsIgnoreCaseAndBindingsRejectCaseCollisions()
    {
        using var profiles = new ProfileStore(Paths, Game.PoE1);
        var id = profiles.Create("One");
        profiles.Switch(id.ToUpperInvariant());
        profiles.Rename(id.ToUpperInvariant(), "Renamed");
        using (var profile = profiles.Open(id.ToUpperInvariant()))
            Assert.Throws<JsonException>(() => profile.Update("hotkeys", x => x.Hotkeys.Add(
                new() { Id = "COPY-ITEM", Key = 120, Action = 0 })));
        profiles.Delete(id.ToUpperInvariant());
        Assert.Single(profiles.Profiles);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
