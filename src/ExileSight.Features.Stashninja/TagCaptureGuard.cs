namespace ExileSight.Features.Stashninja;

/// <summary>Reject stale frames containing the overlay's repeated dark, centered 27-DIP panels.</summary>
public static class TagCaptureGuard
{
    public static bool ContainsPanels(Layout layout, Transform transform, Resolution size, GridSample sample, double? tagScale = null)
    {
        if (sample.Bgra.LongLength != (long)sample.Width * sample.Height * 4) return true;
        double? Row(double x, double y)
        {
            var ix = (int)Math.Round(x) - sample.X; var iy = (int)Math.Round(y) - sample.Y;
            if (ix < 0 || iy < 0 || ix >= sample.Width || iy >= sample.Height) return null;
            var n = (iy * sample.Width + ix) * 4;
            return (sample.Bgra[n] + 2 * sample.Bgra[n + 1] + sample.Bgra[n + 2]) / 4d;
        }
        var panels = 0;
        foreach (var cell in layout.Cells)
        {
            var b = Geometry.TagBounds(layout, cell, transform);
            var h = Math.Min(b.Height, 27 * (tagScale ?? size.Height / 1080d));
            var top = Math.Round(b.Y + (b.Height - h) / 2); var bottom = Math.Round(top + h);
            var support = 0;
            for (var j = 1; j <= 9; j++)
            {
                var x = b.X + b.Width * j / 10;
                var a = Row(x, top - 2); var b0 = Row(x, top + 1);
                var c = Row(x, bottom - 2); var d = Row(x, bottom + 1);
                if (a is null || b0 is null || c is null || d is null) continue;
                if (b0 < a * .6 && c < d * .6 && a - b0 > 6 && d - c > 6) support++;
            }
            if (support >= 6) panels++;
        }
        return panels >= Math.Max(2, (int)Math.Ceiling(layout.Cells.Length * .2));
    }
}
