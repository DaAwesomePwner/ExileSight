using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Platform.Windows.Input;

public sealed partial class HookHost
{
    private PhysicalIssuer? overlayIssuer;
    private Dictionary<string, IFeatureCommandHandler> featureCommands = [];
    public OverlayClickPairing OverlayClicks { get; } = new();
    // App supplies a command resolved from the compiled-in host, never an arbitrary feature-supplied ID.
    public IntentToken? RedeemOverlayClick(nint window, int x, int y, string commandId)
    {
        var click = OverlayClicks.Redeem(window, x, y, clock.Milliseconds);
        if (!featureCommands.TryGetValue(commandId, out var command)) return null;
        if (click is null || command.RequiredAction is null || overlayIssuer is null) return null;
        return overlayIssuer.Issue(click.Timestamp, new HotkeyBinding(command.Command, 0, KeyModifiers.None,
            false, null, FeatureCommand: command.Command, FeatureId: command.FeatureId, CommandAction: command.RequiredAction));
    }
}
