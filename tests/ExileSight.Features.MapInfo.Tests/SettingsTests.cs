using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.MapInfo.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void SettingsRoundTripAndOldDocumentsUseDefaults()
    {
        var context = new Context();
        Assert.Equal(new MapInfoSettings(), MapInfoSettings.Load(context));
        context.Value = JsonSerializer.SerializeToElement(new { });
        Assert.Equal(new MapInfoSettings(), MapInfoSettings.Load(context));
        var chosen = new MapInfoSettings(80, 50, 30);
        chosen.Save(context);
        Assert.Equal("settings", context.Key);
        Assert.Equal(chosen, MapInfoSettings.Load(context));
        Assert.Throws<ArgumentException>(() => new MapInfoSettings(-1).Save(context));
        Assert.Equal(chosen, MapInfoSettings.Load(context));
        context.Value = JsonSerializer.SerializeToElement("invalid");
        Assert.Equal(new MapInfoSettings(), MapInfoSettings.Load(context));
    }

    private sealed class Context : IFeatureContext
    {
        public JsonElement? Value { get; set; }
        public string? Key { get; private set; }
        public JsonElement? ReadSetting(string key) => Value;
        public void WriteSetting(string key, JsonElement value) { Key = key; Value = value; }
        public ActionResult Execute(IntentToken? token, GameAction action) => throw new NotSupportedException();
        public void RegisterCommand(string command, ActionKind? action, Func<IntentToken?, ActionResult> execute) => throw new NotSupportedException();
        public void RegisterOmniHandler(IOmniHandler handler) => throw new NotSupportedException();
        public void Subscribe<T>(Action<T> handler) where T : LogEvent => throw new NotSupportedException();
        public void RegisterOverlay(object viewModel) => throw new NotSupportedException();
    }
}
