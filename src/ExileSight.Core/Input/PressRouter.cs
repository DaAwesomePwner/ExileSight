using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Input;

/// <summary>Queue-consumer routing. Every dequeued physical press produces an observable outcome.
/// Action-declaring commands receive one token; local commands receive none.</summary>
public sealed class PressRouter(HotkeyDispatcher dispatcher, IClock clock, Func<bool> gameFocused,
    Func<IntentToken?, HotkeyBinding, ActionResult> execute,
    Func<IntentToken?, PhysicalPress, ActionResult>? start = null,
    Action<HotkeyBinding, PressReleased>? release = null)
{
    public ActionResult Route(PhysicalPress press)
    {
        try
        {
            if (press.Origin is { } original)
            {
                if (Interlocked.Exchange(ref press.Redeemed, 1) != 0) return new(ActionOutcome.UsedOrUnknownToken);
                if (!original.Executed || !gameFocused() || press.Timestamp < original.Timestamp)
                    return new(ActionOutcome.NotForeground);
                release?.Invoke(press.Binding, new(original.Id, original.Timestamp, press.Timestamp));
                return new(ActionOutcome.Success);
            }
            if (Volatile.Read(ref press.Redeemed) != 0) return new(ActionOutcome.UsedOrUnknownToken);
            var age = clock.Milliseconds - press.Timestamp;
            if (age < 0 || age >= IntentRegistry.LifetimeMilliseconds)
                return Refuse(press, ActionOutcome.Expired);
            if (((press.Binding.Action is not null || press.Binding.CommandAction is not null) || press.Binding.Context == "gameFocused") && !gameFocused())
                return Refuse(press, ActionOutcome.NotForeground);
            if ((press.Binding.Action is not null || press.Binding.CommandAction is not null))
                return Execute(dispatcher.Issue(press), press);
            if (Interlocked.Exchange(ref press.Redeemed, 1) != 0)
                return new(ActionOutcome.UsedOrUnknownToken);
            return Execute(null, press);
        }
        catch (Exception error) { return new(ActionOutcome.Failed, error.Message); }
    }
    private ActionResult Execute(IntentToken? token, PhysicalPress press)
    {
        var result = start is null ? execute(token, press.Binding) : start(token, press);
        press.Executed = result.Outcome == ActionOutcome.Success;
        return result;
    }
    private static ActionResult Refuse(PhysicalPress press, ActionOutcome outcome) =>
        new(Interlocked.Exchange(ref press.Redeemed, 1) == 0 ? outcome : ActionOutcome.UsedOrUnknownToken);
}
