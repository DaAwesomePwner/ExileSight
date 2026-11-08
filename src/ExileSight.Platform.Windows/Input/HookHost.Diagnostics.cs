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
        InputTiming.Record(InputStage.LeaseAge, foreground.Age);
        var stale = foreground.IsStale;
        if (stale && staleNotice.RecordCandidate(dispatcher, key)) ready.Set();
        return dispatcher.Observe(key, true, stale || !foreground.AllowsBlocking());
    }
    private void ReportIgnoredHotkey()
    {
        if (staleNotice.Take() is { } binding)
            report(binding, new(ActionOutcome.NotForeground, StaleFocusNotice.Reason));
    }
}
