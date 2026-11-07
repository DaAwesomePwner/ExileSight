using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class CompositionReviewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultsExplainMissingVersusFailedLoad(bool invalid)
    {
        var root = Path.Combine(Path.GetTempPath(), "ExileSight-defaults-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "settings.json");
            if (invalid) File.WriteAllText(path, "invalid");
            using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), path,
                SettingsJsonContext.Default.SettingsDocument, () => new(), debounce: TimeSpan.FromSeconds(10));
            Assert.True(store.LoadedDefaults);
            Assert.StartsWith(invalid ? "load failed:" : "no file yet", store.DefaultsReason);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void FailedCommitPublishesNeitherDocumentNorWarnings()
    {
        var root = Path.Combine(Path.GetTempPath(), "ExileSight-commit-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "settings.json");
            File.WriteAllText(path, """{"schemaVersion":1}""");
            using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), path,
                SettingsJsonContext.Default.SettingsDocument, () => new(),
                replaceOperation: (_, _, _) => throw new IOException("Injected replace failure"));
            var warnings = store.Warnings.ToArray();
            var original = File.ReadAllBytes(path);
            var changed = 0;
            store.Changed += (_, _) => changed++;
            Assert.Throws<IOException>(() => store.Commit("general", s => s.General.UpdateChannel = "invalid"));
            Assert.Equal(warnings, store.Warnings);
            Assert.Equal("stable", store.Snapshot.General.UpdateChannel);
            Assert.Equal(0, changed);
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally { Directory.Delete(root, true); }
    }
}
