namespace ExileSight.Features.Stashninja;

public sealed record GridIdentification(Layout? Layout, Transform? Transform, double Score, double Margin, string Reason);

/// <summary>Compare all shipped geometries; ambiguity never grants a display identity.</summary>
public static class GridIdentifier
{
    public const double MinimumScore = 25, MinimumMargin = 3;
    public static GridIdentification Identify(IReadOnlyList<Layout> layouts, Resolution size, GridSample sample,
        Func<Layout, Transform?> calibration)
    {
        var candidates = layouts.Take(32).Select(layout =>
        {
            var start = calibration(layout) ?? Geometry.Fit(layout.ReferenceResolution, size);
            return (Layout: layout, Result: GridLocator.Locate(layout, size, start, sample, local: true));
        }).OrderByDescending(c => c.Result.Score).ToArray();
        if (candidates.Length == 0 || candidates[0].Result.Transform is null)
            return new(null, null, 0, 0, "No stash");
        var best = candidates[0];
        var margin = best.Result.Score - (candidates.Length > 1 ? candidates[1].Result.Score : 0);
        if (best.Result.Score < MinimumScore || margin < MinimumMargin)
            return new(null, null, best.Result.Score, margin, "Unknown");
        return new(best.Layout, best.Result.Transform, best.Result.Score, margin, "Identified");
    }
}
