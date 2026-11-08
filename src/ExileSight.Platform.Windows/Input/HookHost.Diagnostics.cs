using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Platform.Windows.Input;

public sealed partial class HookHost
{
    private readonly StaleFocusNotice staleNotice = new();
    private int workerApartment = (int)ApartmentState.Unknown;
    internal ApartmentState ActionWorkerApartment => (ApartmentState)Volatile.Read(ref workerApartment);
    private DispatchDecision ObserveWithFocus(PhysicalKey key)
    {
        InputTiming.Record(InputStage.HookDelay, clock.Milliseconds - key.Timestamp);
        var focus = foreground.Evaluate();
        InputTiming.Record(InputStage.LeaseAge, focus.CachedAge);
        if (focus.Reevaluated)
            InputTiming.Record(InputStage.LeaseReevaluated, focus.CachedAge,
                focus.Allowed ? InputTimingOutcome.Success : InputTimingOutcome.Failed);
        if (!focus.Allowed && focus.Reevaluated && staleNotice.RecordCandidate(dispatcher, key)) ready.Set();
        return dispatcher.Observe(key, true, !focus.Allowed);
    }
    private void ReportIgnoredHotkey()
    {
        if (staleNotice.Take() is { } binding)
            report(binding, new(ActionOutcome.NotForeground, StaleFocusNotice.Reason));
    }
}
