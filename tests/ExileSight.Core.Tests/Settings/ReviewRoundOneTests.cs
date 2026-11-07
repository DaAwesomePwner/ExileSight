using System.Text.Json;
using ExileSight.Core.Settings;
using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class ReviewRoundOneTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-r1-" + Guid.NewGuid());
    private string Seed(string json)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "settings.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Theory]
    [InlineData(999, 118)]
    [InlineData(0, 0)]
    public void RepairingRuntimeSnapshotPreservesConfiguredEnabledFlag(int action, int key)
    {
        var path = Seed($$"""{"schemaVersion":1,"hotkeys":[{"id":"future","key":{{key}},"action":{{action}},"enabled":true}]}""");
        using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), path, SettingsJsonContext.Default.SettingsDocument, () => new());
        var snapshot = store.Snapshot;
        Assert.False(snapshot.Hotkeys[0].Enabled);
        snapshot.Hotkeys[0].Action = ActionKind.CopyItem;
        snapshot.Hotkeys[0].Key = 118;
        store.Update("hotkeys", s => s.Hotkeys = snapshot.Hotkeys);
        store.Flush();
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var binding = json.RootElement.GetProperty("hotkeys")[0];
        Assert.True(binding.GetProperty("enabled").GetBoolean());
        Assert.Equal(action, binding.GetProperty("action").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedSetupSaveDoesNotPublishOrScheduleRetry(bool completed)
    {
        var original = $$$"""{"schemaVersion":1,"general":{"firstRunCompleted":{{{completed.ToString().ToLowerInvariant()}}},"clientTxtPath":null}}""";
        var path = Seed(original);
        var log = Path.Combine(root, "Client.txt");
        File.WriteAllText(log, "synthetic fixture");
        int changed = 0, replaces = 0;
        var time = new ManualSaveTime();
        using (var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), path,
                   SettingsJsonContext.Default.SettingsDocument, () => new(),
                   replaceOperation: (_, _, _) => { replaces++; throw new IOException("Injected failure"); }, timeProvider: time))
        {
            store.Changed += (_, _) => changed++;
            Assert.Throws<IOException>(() => SetupCommit.Finish(store, log));
            Assert.Equal(completed, store.Snapshot.General.FirstRunCompleted);
            Assert.Null(store.Snapshot.General.ClientTxtPath);
            Assert.Equal(0, changed);
            time.Advance(TimeSpan.FromSeconds(3)); // Beyond both debounce and maximum wait.
            store.Flush();
        }
        Assert.Equal(1, replaces); // Neither timer, Flush nor Dispose retries the failed candidate.
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void SuccessfulSetupPublishesOnlyPersistedSettings()
    {
        var path = Seed("""{"schemaVersion":1}""");
        var log = Path.Combine(root, "Client.txt");
        File.WriteAllText(log, "fixture");
        using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), path, SettingsJsonContext.Default.SettingsDocument, () => new());
        var changed = 0;
        store.Changed += (_, _) =>
        {
            using var saved = JsonDocument.Parse(File.ReadAllText(path));
            Assert.True(saved.RootElement.GetProperty("general").GetProperty("firstRunCompleted").GetBoolean());
            changed++;
        };
        SetupCommit.Finish(store, log);
        Assert.Equal(1, changed);
        Assert.Equal(log, store.Snapshot.General.ClientTxtPath);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
