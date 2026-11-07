using System.Text.Json;
using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class BindingRepairTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StoredDocumentWritersPersistRepairAndExplicitEnabledTogether(bool commit, bool enabled)
    {
        var root = Path.Combine(Path.GetTempPath(), "ExileSight-repair-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "settings.json");
            File.WriteAllText(path, $$"""
                {"schemaVersion":1,"hotkeys":[{"id":"repair","key":0,"action":0,"context":"gameFocused","enabled":{{(!enabled).ToString().ToLowerInvariant()}}}]}
                """);
            using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), path,
                SettingsJsonContext.Default.SettingsDocument, () => new());
            void Repair(SettingsDocument stored)
            {
                // Update/Commit supply persisted values. Runtime Snapshot is never an editor model.
                var rebuilt = JsonSerializer.Deserialize(JsonSerializer.Serialize(stored, SettingsJsonContext.Default.SettingsDocument),
                    SettingsJsonContext.Default.SettingsDocument)!;
                rebuilt.Hotkeys[0].Key = 118;
                rebuilt.Hotkeys[0].Enabled = enabled;
                stored.Hotkeys = rebuilt.Hotkeys;
            }
            if (commit) store.Commit("hotkeys", Repair);
            else { store.Update("hotkeys", Repair); store.Flush(); }
            Assert.Equal(enabled, store.Snapshot.Hotkeys[0].Enabled);
            using var saved = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(enabled, saved.RootElement.GetProperty("hotkeys")[0].GetProperty("enabled").GetBoolean());
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void StoredDocumentWriterRepairingKeyOnlyPreservesStoredEnabled()
    {
        var root = Path.Combine(Path.GetTempPath(), "ExileSight-keyrepair-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "settings.json");
            File.WriteAllText(path, """{"schemaVersion":1,"hotkeys":[{"id":"repair","key":0,"action":0,"enabled":true}]}""");
            using var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), path,
                SettingsJsonContext.Default.SettingsDocument, () => new());
            store.Commit("hotkeys", candidate => candidate.Hotkeys[0].Key = 118);
            Assert.True(store.Snapshot.Hotkeys[0].Enabled);
            using var saved = JsonDocument.Parse(File.ReadAllText(path));
            Assert.True(saved.RootElement.GetProperty("hotkeys")[0].GetProperty("enabled").GetBoolean());
        }
        finally { Directory.Delete(root, true); }
    }

}
