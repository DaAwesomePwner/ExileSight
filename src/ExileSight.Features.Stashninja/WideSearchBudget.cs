namespace ExileSight.Features.Stashninja;

/// <summary>Instance-lifetime budget, independent of watcher resets or failed captures.</summary>
public sealed class WideSearchBudget
{
    public static readonly TimeSpan LayoutCooldown = TimeSpan.FromSeconds(30);
    private readonly object sync = new();
    private readonly Dictionary<string, DateTimeOffset> nextAttempt = [];
    private double[]? baselineHeader;
    private bool attemptedBaseline;

    public void ObserveHeader(double[]? header)
    {
        lock (sync)
        {
            if (!PageSignature.Valid(header)) return;
            if (baselineHeader is not null &&
                baselineHeader.Zip(header!).Average(p => Math.Abs(p.First - p.Second)) <= StashWatchPolicy.ChangeThreshold) return;
            baselineHeader = (double[])header!.Clone();
            attemptedBaseline = false;
        }
    }

    public IReadOnlySet<Layout> Reserve(DateTimeOffset now, double[]? header, IReadOnlyList<Layout> candidates)
    {
        lock (sync)
        {
            ObserveHeader(header);
            if (!PageSignature.Valid(header) || attemptedBaseline) return new HashSet<Layout>();
            var allowed = candidates.Where(l => !nextAttempt.TryGetValue(l.TabId + "/" + l.Page, out var next) || now >= next)
                .Take(2).ToHashSet();
            if (allowed.Count == 0) return allowed;
            // Consume before work begins: cancellation or failure cannot refund expensive work.
            attemptedBaseline = true;
            foreach (var layout in allowed) nextAttempt[layout.TabId + "/" + layout.Page] = now + LayoutCooldown;
            return allowed;
        }
    }
}
