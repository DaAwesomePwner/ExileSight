using ExileSight.Core.Settings;
using System.Text.Json.Nodes;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class SaveLifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-lifecycle-" + Guid.NewGuid());
    private string Main => Path.Combine(root, "settings.json");
    private void Seed(string json)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Main, json);
    }

    [Fact]
    public void MigrationKeepsOneTimeOriginalAcrossSavesAndRestarts()
    {
        const string original = """{"schemaVersion":1,"general":{"language":"original"}}""";
        Seed(original);
        var pipeline = new MigrationPipeline(2, [new Migration()]);
        using (var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), Main,
                   SettingsJsonContext.Default.SettingsDocument, () => new(), pipeline))
        {
            store.Flush();
            store.Update("general", x => x.General.Language = "second");
            store.Flush();
        }
        Assert.Equal(original, File.ReadAllText(Main + ".v1.bak"));
        Seed("""{"schemaVersion":1,"general":{"language":"another"}}""");
        using (var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), Main,
                   SettingsJsonContext.Default.SettingsDocument, () => new(), pipeline))
            store.Flush();
        Assert.Equal(original, File.ReadAllText(Main + ".v1.bak"));
    }

    [Fact]
    public async Task SustainedUpdatesCannotPostponeSavePastMaximumWait()
    {
        using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), Main,
            SettingsJsonContext.Default.SettingsDocument, () => new(), debounce: TimeSpan.FromHours(1));
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var updates = 0;
        while (!File.Exists(Main) && DateTime.UtcNow < deadline)
        {
            store.Update("general", x => x.General.Language = (++updates).ToString());
            await Task.Delay(20);
        }
        Assert.True(File.Exists(Main)); // Without maximum wait this would wait an hour.
        Assert.True(updates > 1);
    }

    [Fact]
    public async Task TimerExceptionsAreWarningsAndDisposeRetriesPendingSave()
    {
        Seed("""{"schemaVersion":1}""");
        var attempts = 0;
        var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), Main,
            SettingsJsonContext.Default.SettingsDocument, () => new(), debounce: TimeSpan.FromMilliseconds(10),
            replaceOperation: (source, destination, backup) =>
            {
                if (Interlocked.Increment(ref attempts) == 1) throw new InvalidOperationException("injected failure");
                File.Replace(source, destination, backup);
            });
        try
        {
            store.Update("general", x => x.General.Language = "saved-on-dispose");
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (store.Warnings.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(10);
            Assert.Contains(store.Warnings, x => x.Contains("injected failure"));
        }
        finally { store.Dispose(); }
        Assert.Contains("saved-on-dispose", File.ReadAllText(Main));
    }

    [Fact]
    public void CaseCollidingCatalogIsRecovered()
    {
        Directory.CreateDirectory(Path.Combine(root, "poe1"));
        var path = Path.Combine(root, "poe1", "profiles.json");
        File.WriteAllText(path, """
            {"schemaVersion":1,"game":1,"activeProfileId":"default","profiles":[
              {"id":"default","name":"One"},{"id":"DEFAULT","name":"Two"}]}
            """);
        using var profiles = new ProfileStore(new AppPaths(root), ExileSight.Core.Games.Game.PoE1);
        Assert.NotEmpty(profiles.Warnings);
        Assert.Single(profiles.Profiles);
    }

    private sealed class Migration : IMigration
    {
        public int FromVersion => 1;
        public void Apply(JsonObject document) => document["general"]!["firstRunCompleted"] = true;
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
