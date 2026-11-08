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
    public bool Identified { get; private set; }
    public bool Pinned { get; private set; }
    public TimeSpan Interval => TimeSpan.FromMilliseconds(1800);
    public static bool ShouldIdentify(double score) => score >= GridIdentifier.MinimumScore / 2;
    private static bool Changed(double[] a, double[] b) =>
        a.Length != b.Length || a.Zip(b).Average(p => Math.Abs(p.First - p.Second)) > ChangeThreshold;

    public WatchStep Tick(DateTimeOffset now, double[]? currentHeader, double[]? currentSubTab)
    {
        if (!PageSignature.Valid(currentHeader) ||
            (Identified && subTab is not null && !Pinned && !PageSignature.Valid(currentSubTab)))
            return new(WatchWork.Unknown);
        var headerChanged = header is not null && Changed(header, currentHeader!);
        var changed = headerChanged || (Identified && !Pinned && subTab is not null && Changed(subTab, currentSubTab!));
        if (changed && Identified)
        {
            if (headerChanged) Pinned = false;
            return new(Identified ? WatchWork.Identify : WatchWork.Precheck, true);
        }
        unknown = false;
        if (Identified) return new(WatchWork.None);
        return new(now >= nextCheck ? WatchWork.Precheck : WatchWork.None);
    }

    public void Accept(double[] headerSignature, double[]? subSignature, bool pinned = false)
    {
        header = (double[])headerSignature.Clone();
        subTab = subSignature is null ? null : (double[])subSignature.Clone();
        Identified = true; Pinned = pinned; unknown = false;
    }

    // Returns true only on the displayed -> unknown transition.
    public bool Unknown(DateTimeOffset now, bool changed, double[]? currentHeader)
    {
        if (!unknown) { unknown = true; unknownSince = now; }
        if (Identified && !changed && now - unknownSince < TimeSpan.FromSeconds(1)) return false;
        var hide = Identified;
        Identified = false; Pinned = false;
        if (PageSignature.Valid(currentHeader)) header = (double[])currentHeader!.Clone();
        subTab = null;
        nextCheck = now + Interval;
        return hide;
    }
}
