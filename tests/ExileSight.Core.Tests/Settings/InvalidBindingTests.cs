using System.Text.Json;
using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class InvalidBindingTests
{
    [Theory]
    [InlineData("\"key\":0", "key")]
    [InlineData("\"key\":256", "key")]
    [InlineData("\"modifiers\":32", "modifier")]
    [InlineData("\"context\":\"\"", "context")]
    [InlineData("\"featureCommand\":\"guide.next\"", "exactly one")]
    [InlineData("\"action\":null", "exactly one")]
    [InlineData("\"action\":1,\"text\":null", "text")]
    public void SemanticFailuresDisableOnlyRuntimeBindingAndPreserveStoredValues(string change, string reason)
    {
        var root = Path.Combine(Path.GetTempPath(), "ExileSight-binding-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var binding = System.Text.Json.Nodes.JsonNode.Parse("""{"id":"bad","key":118,"action":0,"enabled":true,"context":"gameFocused","modifiers":0}""")!;
            foreach (var entry in System.Text.Json.Nodes.JsonNode.Parse("{" + change + "}")!.AsObject())
                binding[entry.Key] = entry.Value?.DeepClone();
            var path = Path.Combine(root, "settings.json");
            File.WriteAllText(path, "{\"schemaVersion\":1,\"hotkeys\":[" + binding.ToJsonString() +
                ", {\"id\":\"good\",\"key\":119,\"action\":0,\"enabled\":true}]}");
            using (var store = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), path,
                       SettingsJsonContext.Default.SettingsDocument, () => new()))
            {
                Assert.False(store.Snapshot.Hotkeys[0].Enabled);
                Assert.True(store.Snapshot.Hotkeys[1].Enabled);
                Assert.Contains(store.Warnings, w => w.Contains("bad") && w.Contains(reason));
                store.Update("general", s => s.General.Language = "de");
                store.Flush();
                store.Update("hotkeys", s => s.Hotkeys = store.Snapshot.Hotkeys);
                store.Flush();
            }
            using var saved = JsonDocument.Parse(File.ReadAllText(path));
            var actual = saved.RootElement.GetProperty("hotkeys")[0];
            using var expected = JsonDocument.Parse(binding.ToJsonString());
            foreach (var property in expected.RootElement.EnumerateObject())
                Assert.Equal(property.Value.ToString(), actual.GetProperty(property.Name).ToString());
            Assert.Empty(Directory.GetFiles(root, "*.corrupt-*"));
        }
        finally { Directory.Delete(root, true); }
    }
}
