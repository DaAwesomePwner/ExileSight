namespace ExileSight.Features.Stashninja;

public sealed record GridIdentification(Layout? Layout, Transform? Transform, double Score, double Margin, string Reason, int Evaluations = 0, int BroadSearches = 0);

/// <summary>Compare all shipped geometries; ambiguity never grants a display identity.</summary>
public static class GridIdentifier
{
    public const double MinimumScore = 25, MinimumMargin = 3;
    public static GridIdentification Identify(IReadOnlyList<Layout> layouts, Resolution size, GridSample sample,
        Func<Layout, Transform?> calibration, IReadOnlyDictionary<Layout, double>? precheck = null)
    {
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
            var ranked = candidates.OrderByDescending(c => c.Result.Score).ToArray();
            if (ranked.Length == 0) return new(null, null, 0, 0, "No stash", evaluations, broad);
            var best = ranked[0];
            var margin = best.Result.Score - (ranked.Length > 1 ? ranked[1].Result.Score : 0);
            var accepted = best.Result.Transform is not null && best.Result.Score >= MinimumScore && margin >= MinimumMargin;
            return new(accepted ? best.Layout : null, accepted ? best.Result.Transform : null,
                best.Result.Score, margin, accepted ? "Identified" : "Unknown", evaluations, broad);
        }
        var local = Select();
        if (local.Layout is not null) return local;
        // At most two uncalibrated candidates qualify for the wider first-use search.
        var fallback = Enumerable.Range(0, candidates.Length)
            .Where(i => candidates[i].Saved is null)
            .Select(i => (Index: i, Score: Math.Max(candidates[i].Result.Score,
                precheck?.GetValueOrDefault(candidates[i].Layout) ?? 0)))
            .Where(c => StashWatchPolicy.ShouldIdentify(c.Score))
            .OrderByDescending(c => c.Score).Take(2).ToArray();
        foreach (var candidate in fallback)
        {
            var c = candidates[candidate.Index];
            var result = GridLocator.Locate(c.Layout, size, c.Start, sample);
            evaluations += result.Evaluations; broad++;
            candidates[candidate.Index] = (c.Layout, c.Saved, c.Start, result);
        }
        return Select();
    }
}
