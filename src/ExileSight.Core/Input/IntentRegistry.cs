using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Input;

public sealed class IntentRegistry(IClock clock, object? authority = null)
{
    private readonly Dictionary<IntentToken, Press> pending = [];
    public const long LifetimeMilliseconds = 1500;
    public sealed record Press(long Timestamp, string HotkeyId, ActionKind Action, string FeatureId = "builtin");
    // Only a holder of the registry's construction capability can register a physical intent.
    internal IntentToken Register(object capability, long timestamp, string hotkey, ActionKind action, string featureId = "builtin")
    {
        if (authority is null || !ReferenceEquals(authority, capability))
            throw new InvalidOperationException("Physical-input authority required");
        lock (pending)
        {
            foreach (var old in pending.Where(p => clock.Milliseconds - p.Value.Timestamp >= LifetimeMilliseconds)
                         .Select(p => p.Key).ToArray()) pending.Remove(old);
            var token = new IntentToken();
            pending.Add(token, new(timestamp, hotkey, action, featureId));
            return token;
        }
    }
    public ActionOutcome Consume(IntentToken? token, out Press? press)
    {
        press = null;
        if (token is null) return ActionOutcome.MissingToken;
        lock (pending)
        {
            if (!pending.Remove(token, out press)) return ActionOutcome.UsedOrUnknownToken;
        }
        var age = clock.Milliseconds - press.Timestamp;
        return age < 0 || age >= LifetimeMilliseconds ? ActionOutcome.Expired : ActionOutcome.Success;
    }
}
public sealed class GatePolicy(IntentRegistry registry, IEnumerable<ActionKind> declared,
    IEnumerable<ExileSight.Core.Features.FeatureManifest>? manifests = null)
{
    private readonly Dictionary<string, HashSet<ActionKind>> allowed = Build(declared, manifests);
    private static Dictionary<string, HashSet<ActionKind>> Build(IEnumerable<ActionKind> builtins,
        IEnumerable<ExileSight.Core.Features.FeatureManifest>? manifests)
    {
        var result = new Dictionary<string, HashSet<ActionKind>>(StringComparer.Ordinal) { ["builtin"] = builtins.ToHashSet() };
        foreach (var manifest in manifests ?? []) { manifest.Validate(); result.Add(manifest.Id, manifest.GameActions.ToHashSet()); }
        return result;
    }
    public ActionOutcome Validate(IntentToken? token, GameAction action, bool foreground, out IntentRegistry.Press? press, string? featureId = null)
    {
        var result = registry.Consume(token, out press);
        if (result != ActionOutcome.Success) return result;
        if (featureId is not null && press!.FeatureId != featureId) return ActionOutcome.Undeclared;
        if (press!.Action != action.Kind) return ActionOutcome.WrongAction;
        if (!foreground) return ActionOutcome.NotForeground;
        return allowed.TryGetValue(press.FeatureId, out var actions) && actions.Contains(action.Kind) ? ActionOutcome.Success : ActionOutcome.Undeclared;
    }
}
