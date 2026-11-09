namespace ExileSight.Features.Stashninja;

public enum WatchWork { None, Precheck, Identify, Unknown }
public sealed record WatchStep(WatchWork Work, bool Changed = false);

/// <summary>Baseline comparison and retry policy, independent of capture, UI and clocks.</summary>
public sealed class StashWatchPolicy
{
    public const double ChangeThreshold = .04;
    private double[]? header, subTab;
    private DateTimeOffset nextCheck, unknownSince;
    private bool unknown;
    private double[]? pendingHeader, pendingSub;
    private double[]? acknowledgedHeader, acknowledgedSub;
    private bool acknowledgementChanged;
    private DateTimeOffset? confirmAfter;
    private int keepFailures, unstableTicks, passingConfirmations;
    private bool confirmationFailed;
    private DateTimeOffset lastFailure;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromSeconds(4);
    public bool Identified { get; private set; }
    public bool Pinned { get; private set; }
    public TimeSpan Interval => TimeSpan.FromMilliseconds(1800);
    public static bool ShouldIdentify(double score) => score >= 12.5;
    private static bool Changed(double[] a, double[] b) =>
        a.Length != b.Length || a.Zip(b).Average(p => Math.Abs(p.First - p.Second)) > ChangeThreshold;

    public WatchStep Tick(DateTimeOffset now, double[]? currentHeader, double[]? currentSubTab)
    {
        if (keepFailures > 0 && now - lastFailure > FailureWindow) keepFailures = 0;
        if (!PageSignature.Valid(currentHeader) ||
            (Identified && subTab is not null && !Pinned && !PageSignature.Valid(currentSubTab)))
            return new(WatchWork.Unknown);
        if (Identified && confirmAfter is { } due)
            return new(now > due ? WatchWork.Identify : WatchWork.None);
        if (Identified && acknowledgedHeader is not null)
        {
            if (!Changed(acknowledgedHeader, currentHeader!) && SameOptional(acknowledgedSub, currentSubTab))
                return new(WatchWork.None);
            acknowledgedHeader = acknowledgedSub = null;
            acknowledgementChanged = true;
        }
        var headerChanged = header is not null && Changed(header, currentHeader!);
        var changed = headerChanged || (Identified && !Pinned && subTab is not null && Changed(subTab, currentSubTab!));
        if ((changed || acknowledgementChanged || keepFailures > 0) && Identified)
        {
            if (headerChanged) Pinned = false;
            var stable = pendingHeader is not null && !Changed(pendingHeader, currentHeader!) &&
                (currentSubTab is null || pendingSub is not null && !Changed(pendingSub, currentSubTab));
            pendingHeader = (double[])currentHeader!.Clone();
            pendingSub = currentSubTab is null ? null : (double[])currentSubTab.Clone();
            var check = stable || ++unstableTicks >= 2;
            if (check) unstableTicks = 0;
            return new(check ? WatchWork.Identify : WatchWork.None, true);
        }
        unknown = false; pendingHeader = pendingSub = null; unstableTicks = 0;
        if (Identified) return new(WatchWork.None);
        return new(now >= nextCheck ? WatchWork.Precheck : WatchWork.None);
    }

    private static bool SameOptional(double[]? a, double[]? b) =>
        a is null ? b is null : b is not null && !Changed(a, b);

    // A newly adopted automatic baseline needs an independent capture one tick later.
    public void ObserveIdentification(DateTimeOffset completedAt, double[] currentHeader, double[]? currentSub)
    {
        var changed = !Identified || header is null || Changed(header, currentHeader) || !SameOptional(subTab, currentSub) ||
            acknowledgedHeader is not null && (Changed(acknowledgedHeader, currentHeader) || !SameOptional(acknowledgedSub, currentSub));
        var confirmsHover = confirmAfter is { } due && completedAt > due && acknowledgedHeader is not null &&
            !Changed(acknowledgedHeader, currentHeader) && SameOptional(acknowledgedSub, currentSub);
        var matching = confirmsHover || acknowledgedHeader is null && !changed;
        var pending = confirmAfter is not null && !Confirm(completedAt, matching);
        var failed = confirmationFailed; var passes = passingConfirmations;
        Accept(currentHeader, currentSub);
        if (pending || changed && !confirmsHover)
        {
            confirmAfter = completedAt;
            confirmationFailed = failed; passingConfirmations = passes;
        }
    }

    public void Accept(double[] headerSignature, double[]? subSignature, bool pinned = false)
    {
        confirmationFailed = false; passingConfirmations = 0;
        confirmAfter = null; acknowledgedHeader = acknowledgedSub = null; acknowledgementChanged = false;
        header = (double[])headerSignature.Clone();
        subTab = subSignature is null ? null : (double[])subSignature.Clone();
        Identified = true; Pinned = pinned; unknown = false;
        pendingHeader = pendingSub = null; keepFailures = 0; unstableTicks = 0;
    }

    public bool Retain(DateTimeOffset now, bool supported, double[]? currentHeader, double[]? currentSub, bool adoptBaseline = true)
    {
        if (supported)
        {
            if (adoptBaseline && PageSignature.Valid(currentHeader)) ObserveIdentification(now, currentHeader!, currentSub);
            if (!adoptBaseline)
            {
                var sameAcknowledgement = acknowledgedHeader is not null && currentHeader is not null &&
                    !Changed(acknowledgedHeader, currentHeader) && SameOptional(acknowledgedSub, currentSub);
                var matchesNormal = acknowledgedHeader is null && header is not null && currentHeader is not null &&
                    !Changed(header, currentHeader) && SameOptional(subTab, currentSub);
                // Owner decision: a new hover signature is provisional too. Either passing
                // identification mode can confirm the same signature on a later capture.
                var confirmed = Confirm(now, sameAcknowledgement || matchesNormal);
                var alreadyTrusted = confirmAfter is null && sameAcknowledgement;
                acknowledgedHeader = currentHeader is null ? null : (double[])currentHeader.Clone();
                acknowledgedSub = currentSub is null ? null : (double[])currentSub.Clone();
                confirmAfter = confirmed || alreadyTrusted ? null : now;
                if (confirmed) { confirmationFailed = false; passingConfirmations = 0; }
                pendingHeader = pendingSub = null; unstableTicks = 0; acknowledgementChanged = false;
            }
            keepFailures = 0;
            return false;
        }
        if (confirmAfter is not null)
        { confirmAfter = now; confirmationFailed = true; passingConfirmations = 0; }
        if (now - lastFailure > FailureWindow) keepFailures = 0;
        lastFailure = now;
        return ++keepFailures >= 2 && Unknown(now, true, currentHeader);
    }

    private bool Confirm(DateTimeOffset completedAt, bool matching)
    {
        if (!matching) passingConfirmations = 0;
        if (confirmAfter is not { } due || completedAt <= due || !matching) return false;
        // A failed confirmation needs two subsequent independent passes, not one stale rebound.
        return !confirmationFailed || ++passingConfirmations >= 2;
    }

    // Returns true only on the displayed -> unknown transition.
    public bool Unknown(DateTimeOffset now, bool changed, double[]? currentHeader)
    {
        if (!unknown) { unknown = true; unknownSince = now; }
        if (Identified && !changed && now - unknownSince < TimeSpan.FromSeconds(1)) return false;
        var hide = Identified;
        Identified = false; Pinned = false; confirmAfter = null;
        confirmationFailed = false; passingConfirmations = 0;
        acknowledgedHeader = acknowledgedSub = null; acknowledgementChanged = false;
        if (PageSignature.Valid(currentHeader)) header = (double[])currentHeader!.Clone();
        subTab = null;
        nextCheck = now + Interval;
        return hide;
    }
}
