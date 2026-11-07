using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class ProfileTests
{
    [Fact]
    public void ClassRulesPrecedeGlobalAndImplicitsAreSeparate()
    {
        var item = CalculatorTests.Hybrid();
        var settings = new ItemInfoSettings([new("Test", [
            new("Armour", Highlight.Undesired), new("Armour", Highlight.Desired, "Body Armours"),
            new("Resistance", Highlight.Desired, Implicit: true)])]);
        Assert.Equal(Highlight.Desired, settings.Evaluate(item, item.Mods[0]));
        Assert.Equal(Highlight.None, settings.Evaluate(item, item.Mods[2]));
        Assert.Equal(Highlight.Desired, settings.Evaluate(item, item.Mods[2] with { Kind = ModKind.Implicit }));
    }

    [Fact]
    public void ProfilesRoundTripThroughFeatureSettingsAndEnforceLimit()
    {
        var context = new Context();
        var settings = new ItemInfoSettings(Enumerable.Range(0, 5)
            .Select(i => new HighlightProfile($"Profile {i}", [new("Life", Highlight.Desired)])).ToArray(), 4);
        settings.Save(context);
        var loaded = ItemInfoSettings.Load(context);
        Assert.Equal(5, loaded.Profiles.Length);
        Assert.Equal(4, loaded.ActiveProfile);
        Assert.Equal(settings.Profiles[4].Rules, loaded.Profiles[4].Rules);
        Assert.Throws<ArgumentException>(() => (settings with { Profiles = [.. settings.Profiles, settings.Profiles[0]] }).Save(context));
        context.Value = JsonSerializer.SerializeToElement(new { profiles = "invalid" });
        Assert.Empty(ItemInfoSettings.Load(context).Profiles);
    }

    [Fact]
    public void FeatureRegistersPassiveOmniAndObservableViewModel()
    {
        var feature = new ItemInfoFeature();
        var context = new Context();
        feature.Register(context);
        Assert.Empty(feature.Manifest.GameActions);
        Assert.Same(feature.ViewModel, context.Overlay);
        Assert.True(context.Handler!.Priority > 0);
        var changes = 0;
        feature.ViewModel.PropertyChanged += (_, _) => changes++;
        context.Handler.Handle(CalculatorTests.Hybrid(), new(Game.PoE1), OmniPressKind.Short);
        Assert.NotNull(feature.ViewModel.Current);
        feature.ViewModel.Close();
        Assert.Null(feature.ViewModel.Current);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void MarkersStaySeparateAndUnidentifiedCountsAreUnknown()
    {
        var item = ItemTextParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures/markers.txt")), Game.PoE1).Item!;
        var result = new ItemInfoCalculator().Calculate(item);
        Assert.Single(result.Mods, m => m.Source.Kind.HasFlag(ModKind.Implicit));
        Assert.Single(result.Mods, m => m.Source.Kind.HasFlag(ModKind.Enchant));
        Assert.Single(result.Mods, m => m.TierLabel == "Crafted");
        Assert.Contains(result.Mods, m => m.TierLabel == "Unique");
        Assert.Null(result.Prefixes);
        Assert.Null(result.UniqueDropTier);
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
