using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Logs;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;
namespace ExileSight.App.Shell.Pages;

// Settings-only adapter inside one validated store transaction. No input authority.
internal sealed class OverlaySettingsContext(SettingsDocument document) : IFeatureContext
{
    public JsonElement? ReadSetting(string key) => document.Features.Sections.TryGetValue("mapinfo", out var section)
        && section.TryGetValue(key, out var value) ? value : null;
    public void WriteSetting(string key, JsonElement value)
    {
        if (!document.Features.Sections.TryGetValue("mapinfo", out var section))
            document.Features.Sections["mapinfo"] = section = [];
        section[key] = value;
    }
    public ActionResult Execute(IntentToken? token, GameAction action) => throw new NotSupportedException();
    public void RegisterCommand(string command, ActionKind? action, Func<IntentToken?, ActionResult> execute) => throw new NotSupportedException();
    public void RegisterOmniHandler(IOmniHandler handler) => throw new NotSupportedException();
    public void Subscribe<T>(Action<T> handler) where T : LogEvent => throw new NotSupportedException();
    public void RegisterOverlay(object viewModel) => throw new NotSupportedException();
}
