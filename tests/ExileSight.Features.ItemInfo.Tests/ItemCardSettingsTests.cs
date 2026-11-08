using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class ItemCardSettingsTests
{
    [Fact]
    public void OldJsonDefaultsAllCardSectionsOnAndNewTogglesRoundTrip()
    {
        var context = new Context { Value = JsonSerializer.SerializeToElement(new { Profiles = new object[0], ActiveProfile = 0 }) };
        var old = ItemInfoSettings.Load(context);
        Assert.True(old.ShowCompare);
        Assert.True(old.ShowDefenceBasePercentile);
        Assert.True(old.ShowItemLevelColumn);
        Assert.True(old.ShowImplicits);
        var changed = old with { ShowCompare = false, ShowDefenceBasePercentile = false,
            ShowItemLevelColumn = false, ShowImplicits = false };
        changed.Save(context);
        var loaded = ItemInfoSettings.Load(context);
        Assert.False(loaded.ShowCompare);
        Assert.False(loaded.ShowDefenceBasePercentile);
        Assert.False(loaded.ShowItemLevelColumn);
        Assert.False(loaded.ShowImplicits);
        var compare = new ItemCardComparison([], "note");
        var result = new ItemInfoCalculator().Calculate(CalculatorTests.Hybrid());
        Assert.Same(compare, ItemCardBuilder.Build(result, comparison: compare).Compare);
        Assert.Null(ItemCardBuilder.Build(result, settings: loaded, comparison: compare).Compare);
    }
    private sealed class Context : IFeatureContext
    {
        public JsonElement? Value;
        public IOmniHandler? Handler;
        public object? Overlay;
        public JsonElement? ReadSetting(string key) => Value;
        public void WriteSetting(string key, JsonElement value) => Value = value;
        public void RegisterOmniHandler(IOmniHandler handler) => Handler = handler;
        public void RegisterOverlay(object viewModel) => Overlay = viewModel;
        public void Subscribe<T>(Action<T> handler) where T : LogEvent => throw new NotSupportedException();
        public ActionResult Execute(IntentToken? token, GameAction action) => throw new NotSupportedException();
        public void RegisterCommand(string command, ActionKind? action, Func<IntentToken?, ActionResult> execute) => throw new NotSupportedException();
    }
}
