using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Input;

public sealed class IntentRegistry(IClock clock)
{
    private readonly Dictionary<IntentToken, Press> pending = [];
    public const long LifetimeMilliseconds = 1500;
    public sealed record Press(long Timestamp, string HotkeyId, ActionKind Action);
    // Owned by the physical-input dispatcher, never registered as a feature service.
    internal IntentToken Issue(long timestamp, string hotkey, ActionKind action)
    {
        lock (pending)
        {
            foreach (var old in pending.Where(p => clock.Milliseconds - p.Value.Timestamp >= LifetimeMilliseconds)
                         .Select(p => p.Key).ToArray()) pending.Remove(old);
            var token = new IntentToken();
            pending.Add(token, new(timestamp, hotkey, action));
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
public sealed class GatePolicy(IntentRegistry registry, IEnumerable<ActionKind> declared)
{
    private readonly HashSet<ActionKind> allowed = declared.ToHashSet();
    public ActionOutcome Validate(IntentToken? token, GameAction action, bool foreground, out IntentRegistry.Press? press)
    {
        var result = registry.Consume(token, out press);
        if (result != ActionOutcome.Success) return result;
        if (press!.Action != action.Kind) return ActionOutcome.WrongAction;
        if (!foreground) return ActionOutcome.NotForeground;
        return allowed.Contains(action.Kind) ? ActionOutcome.Success : ActionOutcome.Undeclared;
    }
}
