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
    public void RepairAndEnabledChangePersistTogether(bool commit, bool enabled)
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
            var snapshot = store.Snapshot;
            snapshot.Hotkeys[0].Key = 118;
            snapshot.Hotkeys[0].Enabled = enabled;
            void Repair(SettingsDocument settings) => settings.Hotkeys = snapshot.Hotkeys;
            if (commit) store.Commit("hotkeys", Repair);
            else { store.Update("hotkeys", Repair); store.Flush(); }
            Assert.Equal(enabled, store.Snapshot.Hotkeys[0].Enabled);
            using var saved = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(enabled, saved.RootElement.GetProperty("hotkeys")[0].GetProperty("enabled").GetBoolean());
        }
        finally { Directory.Delete(root, true); }
    }
}
