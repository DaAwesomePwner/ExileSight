using ExileSight.Features.Stashninja;
using Bounds = ExileSight.Features.Stashninja.Geometry.Bounds;

namespace ExileSight.App.Features.Stashninja;

public static class StashSummaryPlacement
{
    public static Bounds? Find(Layout layout, Transform transform, Resolution resolution, double scale,
        double width, double height, RecognitionRegion recognition)
    {
        var excluded = new List<Bounds> { recognition.Map(resolution) };
        foreach (var (key, region) in recognition.SubTabs)
        {
            // Passive header/sub-tab reads use Fit, independent of grid calibration.
            excluded.Add(recognition.Map(resolution, region));
            if (!key.StartsWith("currency/", StringComparison.Ordinal)) continue;
            // CurrencyButtons.Identify uses the calibrated grid transform instead.
            var start = transform.Apply(new(region.X, region.Y));
            excluded.Add(new(start.X, start.Y, region.Width * transform.Scale, region.Height * transform.Scale));
        }
        var right = layout.Cells.Max(c => transform.Apply(Layouts.Origin(layout, c)).X + c.W * transform.Scale);
        var top = layout.Cells.Min(c => transform.Apply(Layouts.Origin(layout, c)).Y);
        var left = layout.Cells.Min(c => transform.Apply(Layouts.Origin(layout, c)).X);
        var bottom = layout.Cells.Max(c => transform.Apply(Layouts.Origin(layout, c)).Y + c.H * transform.Scale);
        var gap = 20 * scale;
        var x = Math.Clamp(left, 0, Math.Max(0, resolution.Width - width));
        var y = Math.Clamp(top, 0, Math.Max(0, resolution.Height - height));
        Bounds[] candidates = [new(right + gap, y, width, height), new(left - gap - width, y, width, height),
            new(x, bottom + gap, width, height), new(x, top - gap - height, width, height)];
        return candidates.FirstOrDefault(b => b.X >= 0 && b.Y >= 0 &&
            b.X + b.Width <= resolution.Width && b.Y + b.Height <= resolution.Height &&
            !excluded.Any(region => RecognitionRegion.Intersects(region, b)));
    }
}
