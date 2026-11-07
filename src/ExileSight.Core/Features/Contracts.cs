using System.Text.Json;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Features;

public interface IFeature
{
    FeatureManifest Manifest { get; }
    void Register(IFeatureContext context);
}
public interface IFeatureContext
{
    ActionResult Execute(IntentToken? token, GameAction action);
    void RegisterCommand(string command, ActionKind? action, Func<IntentToken?, ActionResult> execute);
    void RegisterPressCommand(string command, ActionKind action,
        Func<IntentToken?, PressStarted, ActionResult> press, Action<PressReleased> release) =>
        throw new NotSupportedException();
    void RegisterDefaultBinding(FeatureDefaultBinding binding) => throw new NotSupportedException();
    IReadOnlyList<IOmniHandler> OmniHandlers => [];
    void DispatchOmni(ParsedItem item, OmniContext context, OmniPressKind press) =>
        throw new NotSupportedException();
    void RegisterOmniHandler(IOmniHandler handler);
    void Subscribe<T>(Action<T> handler) where T : LogEvent;
    void RegisterOverlay(object viewModel);
    void RegisterTooltipSection(ITooltipSectionProvider provider) => throw new NotSupportedException();
    IReadOnlyList<ITooltipSectionProvider> TooltipSections => [];
    JsonElement? ReadSetting(string key);
    void WriteSetting(string key, JsonElement value);
}
public enum OmniPressKind { Short, Long }
public sealed record OmniContext(Game Game, string? Area = null);
public interface IOmniHandler
{
    int Priority { get; }
    bool Matches(ParsedItem item, OmniContext context);
    void Handle(ParsedItem item, OmniContext context, OmniPressKind press);
}
