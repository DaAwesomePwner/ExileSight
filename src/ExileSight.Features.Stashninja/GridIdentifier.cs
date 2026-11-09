namespace ExileSight.Features.Stashninja;

public sealed record GridIdentification(Layout? Layout, Transform? Transform, double Score, double Margin, string Reason, int Evaluations = 0, int BroadSearches = 0, int ScorePasses = 0, int ScoreSampleReads = 0);

public sealed record GridCandidateEvidence(Layout Layout, GridLocation Location, bool Wide);

/// <summary>Compare all shipped geometries; ambiguity never grants a display identity.</summary>
public static class GridIdentifier
{
    public const double MinimumScore = 16, MinimumMargin = 3;
    public static GridIdentification Identify(IReadOnlyList<Layout> layouts, Resolution size, GridSample sample,
        Func<Layout, Transform?> calibration, IReadOnlyDictionary<Layout, double>? precheck = null,
        Func<IReadOnlyList<Layout>, IReadOnlySet<Layout>>? reserveWideSearch = null, CurrencyButtons? buttons = null,
        Action<GridCandidateEvidence>? observe = null)
    {
        if (layouts.Any(a => layouts.Any(b => !ReferenceEquals(a, b) && a.ReferenceResolution == b.ReferenceResolution && a.Cells.SequenceEqual(b.Cells))))
            return new(null, null, 0, 0, "Ambiguous geometry");
        var candidates = layouts.Select(layout =>
        {
            var saved = calibration(layout);
            var start = saved ?? Geometry.Fit(layout.ReferenceResolution, size);
            var result = GridLocator.Locate(layout, size, start, sample, local: true);
            return (Layout: layout, Saved: saved, Start: start, LocalScore: result.Score, Result: result, Wide: false);
        }).ToArray();
        if (observe is not null)
            foreach (var c in candidates) observe(new(c.Layout, c.Result, false));
        var evaluations = candidates.Sum(c => c.Result.Evaluations);
        var broad = 0;
        var passes = candidates.Sum(c => c.Result.ScorePasses);
        var reads = candidates.Sum(c => c.Result.ScoreSampleReads);
        GridIdentification Select()
        {
            var ranked = candidates.OrderByDescending(c => c.Result.Score).ToArray();
            if (ranked.Length == 0) return new(null, null, 0, 0, "No stash", evaluations, broad, passes, reads);
            var best = ranked[0];
            var margin = best.Result.Score - (ranked.Length > 1 ? ranked[1].Result.Score : 0);
            var strongestOtherLocal = candidates.Where(c => c.Layout != best.Layout).Select(c => c.LocalScore).DefaultIfEmpty(0).Max();
            var accepted = best.Result.Transform is not null && best.Result.Score >= MinimumScore && margin >= MinimumMargin &&
                (!best.Wide || GridWideAcceptance.Accepts(best.Result, Geometry.Fit(best.Layout.ReferenceResolution, size), strongestOtherLocal));
            // Buttons may confirm the grid's winner, never select a different sibling.
            if (best.Layout.TabId == "currency" && accepted)
            {
                var sibling = ranked.Where(c => c.Layout.TabId == "currency").Skip(1).FirstOrDefault();
                var siblingMargin = best.Result.Score - (sibling.Layout is null ? 0 : sibling.Result.Score);
                var button = buttons?.Identify(size, sample, best.Result.Transform) ?? (Page: (string?)null, Margin: 0d);
                if (siblingMargin < MinimumMargin || button.Page != best.Layout.Page)
                    return new(null, null, best.Result.Score, Math.Min(margin, siblingMargin), "Currency highlight disagrees", evaluations, broad, passes, reads);
                margin = Math.Min(margin, button.Margin);
            }
            return new(accepted ? best.Layout : null, accepted ? best.Result.Transform : null,
                best.Result.Score, margin, accepted ? "Identified" : "Unknown", evaluations, broad, passes, reads);
        }
        var local = Select();
        if (local.Layout is not null) return local;
        // At most two uncalibrated candidates qualify for the wider first-use search.
        var fallback = Enumerable.Range(0, candidates.Length)
            .Where(i => candidates[i].Saved is null)
            .Where(i => StashWatchPolicy.ShouldIdentify(Math.Max(candidates[i].Result.Score,
                precheck?.GetValueOrDefault(candidates[i].Layout) ?? 0)))
            .Select(i => (Index: i, Score: candidates[i].Result.Score +
                .1 * (precheck?.GetValueOrDefault(candidates[i].Layout) ?? 0)))
            .OrderByDescending(c => c.Score).Take(2).ToArray();
        var allowed = reserveWideSearch?.Invoke(fallback.Select(c => candidates[c.Index].Layout).ToArray());
        foreach (var candidate in fallback)
        {
            var c = candidates[candidate.Index];
            if (allowed is not null && !allowed.Contains(c.Layout)) continue;
            var result = GridLocator.Locate(c.Layout, size, c.Start, sample);
            observe?.Invoke(new(c.Layout, result, true));
            evaluations += result.Evaluations; passes += result.ScorePasses; reads += result.ScoreSampleReads; broad++;
            candidates[candidate.Index] = (c.Layout, c.Saved, c.Start, c.LocalScore, result, true);
        }
        return Select();
    }
}
