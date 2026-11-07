using System.Text.Json;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Features;

public enum CommandKind { Bindable, OverlayOnly }
public interface IFeatureCommandPolicy
{
    CommandKind Kind { get; }
}

public interface IFeature
{
    FeatureManifest Manifest { get; }
    void Register(IFeatureContext context);
}
public interface IFeatureContext
{
    void ObserveCopiedItems(Action<CopiedItem> observer) => throw new NotSupportedException();
    IPackReader Packs => EmptyPackReader.Instance;
    ActionResult Execute(IntentToken? token, GameAction action);
    void RegisterCommand(string command, ActionKind? action, Func<IntentToken?, ActionResult> execute);
    void RegisterCommand(string command, ActionKind? action, Func<IntentToken?, ActionResult> execute,
        CommandKind kind)
    {
        if (kind != CommandKind.Bindable) throw new NotSupportedException("Command policy is not supported");
        RegisterCommand(command, action, execute);
    }
    // Null action means a local hold: press/release callbacks receive no input authority.
    void RegisterPressCommand(string command, ActionKind? action,
        Func<IntentToken?, PressStarted, ActionResult> press, Action<PressReleased> release) =>
        throw new NotSupportedException();
    void RegisterDefaultBinding(FeatureDefaultBinding binding) => throw new NotSupportedException();
    IReadOnlyList<IOmniHandler> OmniHandlers => [];
    void DispatchFeature(Action work) => work();
    void DispatchOmni(ParsedItem item, OmniContext context, OmniPressKind press) =>
        throw new NotSupportedException();
    void RegisterOmniHandler(IOmniHandler handler);
    void Subscribe<T>(Action<T> handler) where T : LogEvent;
    void SubscribeLog(Action<ClientLogEvent> handler, LogReplay? replay = null) =>
        throw new NotSupportedException("This host does not provide envelope subscriptions.");
    void RegisterOverlay(object viewModel);
    void RegisterTooltipSection(ITooltipSectionProvider provider) => throw new NotSupportedException();
    IReadOnlyList<ITooltipSectionProvider> TooltipSections => [];
    void RegisterItemMenu(IItemMenuProvider provider) => throw new NotSupportedException();
    IReadOnlyList<IItemMenuProvider> ItemMenus => [];
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
