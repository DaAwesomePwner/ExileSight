using ExileSight.Core.Settings;
using ExileSight.Core.Status;
using Xunit;
namespace ExileSight.Transparency.Tests;

public sealed class TriggerNameTests
{
    [Fact]
    public void EveryRegisteredCommandAndDefaultBindingHasAName()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "trigger-names-" + Guid.NewGuid().ToString("N"));
        try
        {
            var host = FeatureInventory.Read(TransparencyTests.FindRoot(), scratch);
            var ids = host.Commands.Select(c => c.Command).Concat(host.DefaultBindings.Select(b => b.Id))
                .Concat(new SettingsDocument().Hotkeys.Select(b => b.Id));
            foreach (var id in ids) Assert.True(TriggerNames.All.ContainsKey(id), "Missing trigger name: " + id);
        }
        finally { if (Directory.Exists(scratch)) Directory.Delete(scratch, true); }
    }
}
