namespace ExileSight.Features.Stashninja;

public sealed record GridIdentification(Layout? Layout, Transform? Transform, double Score, double Margin, string Reason, int Evaluations = 0, int BroadSearches = 0);

/// <summary>Compare all shipped geometries; ambiguity never grants a display identity.</summary>
public static class GridIdentifier
{
    public const double MinimumScore = 24, MinimumMargin = 3;
    public static GridIdentification Identify(IReadOnlyList<Layout> layouts, Resolution size, GridSample sample,
        Func<Layout, Transform?> calibration, IReadOnlyDictionary<Layout, double>? precheck = null,
        Func<IReadOnlyList<Layout>, IReadOnlySet<Layout>>? reserveWideSearch = null, CurrencyButtons? buttons = null)
    {
        if (layouts.Any(a => layouts.Any(b => !ReferenceEquals(a, b) && a.ReferenceResolution == b.ReferenceResolution && a.Cells.SequenceEqual(b.Cells))))
            return new(null, null, 0, 0, "Ambiguous geometry");
        var currency = buttons?.Identify(size, sample) ?? (Page: (string?)null, Margin: 0d);
        var candidates = layouts.Select(layout =>
        {
            var saved = calibration(layout);
            var start = saved ?? Geometry.Fit(layout.ReferenceResolution, size);
            return (Layout: layout, Saved: saved, Start: start,
                Result: GridLocator.Locate(layout, size, start, sample, local: true));
        }).ToArray();
        var evaluations = candidates.Sum(c => c.Result.Evaluations);
        var broad = 0;
        GridIdentification Select()
        {
            var aligned = candidates.Where(c => c.Layout.TabId == "currency" && c.Result.Transform is not null)
                .Select(c => (Candidate: c, Button: buttons?.Identify(size, sample, c.Result.Transform) ?? (Page: (string?)null, Margin: 0d)))
                .Where(c => c.Button.Page == c.Candidate.Layout.Page).OrderByDescending(c => c.Candidate.Result.Score).ToArray();
            if (aligned.Length > 0) currency = aligned[0].Button;
            var ranked = candidates.Where(c => c.Layout.TabId != "currency" || c.Layout.Page == currency.Page).OrderByDescending(c => c.Result.Score).ToArray();
            if (ranked.Length == 0) return new(null, null, 0, 0, "No stash", evaluations, broad);
            var best = ranked[0];
            var margin = best.Result.Score - (ranked.Length > 1 ? ranked[1].Result.Score : 0);
            if (best.Layout.TabId == "currency") margin = Math.Min(margin, currency.Margin);
            var accepted = best.Result.Transform is not null && best.Result.Score >= MinimumScore && margin >= MinimumMargin;
            return new(accepted ? best.Layout : null, accepted ? best.Result.Transform : null,
                best.Result.Score, margin, accepted ? "Identified" : "Unknown", evaluations, broad);
        }
        var local = Select();
        if (local.Layout is not null) return local;
        // At most two uncalibrated candidates qualify for the wider first-use search.
        var fallback = Enumerable.Range(0, candidates.Length)
            .Where(i => candidates[i].Saved is null && (candidates[i].Layout.TabId != "currency" || currency.Page is null || candidates[i].Layout.Page == currency.Page))
            .Select(i => (Index: i, Score: Math.Max(candidates[i].Result.Score,
                precheck?.GetValueOrDefault(candidates[i].Layout) ?? 0)))
            .Where(c => StashWatchPolicy.ShouldIdentify(c.Score))
            .OrderByDescending(c => c.Score).Take(2).ToArray();
        var allowed = reserveWideSearch?.Invoke(fallback.Select(c => candidates[c.Index].Layout).ToArray());
        foreach (var candidate in fallback)
        {
            var c = candidates[candidate.Index];
            if (allowed is not null && !allowed.Contains(c.Layout)) continue;
            var result = GridLocator.Locate(c.Layout, size, c.Start, sample);
            evaluations += result.Evaluations; broad++;
            candidates[candidate.Index] = (c.Layout, c.Saved, c.Start, result);
        }
        return Select();
    }
}
