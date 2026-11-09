namespace ExileSight.Features.Stashninja;

/// <summary>Fixed-transform evidence to retain an identity, never to select a new page.</summary>
public static class GridKeep
{
    public const double Threshold = 15;
    public static GridArea Bounds(Layout layout, Transform transform, Resolution size, CurrencyButtons? buttons)
    {
        var rectangles = layout.Cells.Select(c =>
        {
            var p = transform.Apply(Layouts.Origin(layout, c));
            return new Geometry.Bounds(p.X - 5 * transform.Scale, p.Y - 5 * transform.Scale,
                (c.W + 10) * transform.Scale, (c.H + 10) * transform.Scale);
        }).ToList();
        if (layout.TabId == "currency" && buttons is not null)
            rectangles.AddRange(buttons.Buttons.Values.Select(b =>
            {
                var p = transform.Apply(new(b.X, b.Y));
                return new Geometry.Bounds(p.X, p.Y, b.Width * transform.Scale, b.Height * transform.Scale);
            }));
        var x = (int)Math.Clamp(Math.Floor(rectangles.Min(b => b.X)), 0, size.Width);
        var y = (int)Math.Clamp(Math.Floor(rectangles.Min(b => b.Y)), 0, size.Height);
        return new(x, y, (int)Math.Clamp(Math.Ceiling(rectangles.Max(b => b.X + b.Width)), x, size.Width) - x,
            (int)Math.Clamp(Math.Ceiling(rectangles.Max(b => b.Y + b.Height)), y, size.Height) - y);
    }
    public static bool Supports(Layout layout, Transform transform, Resolution size, GridSample? sample, CurrencyButtons? buttons)
    {
        if (sample is null || Score(layout, transform, sample) < Threshold) return false;
        return layout.TabId != "currency" || buttons is null ||
            !GridKeepButtons.Contradicts(layout.Page, transform, sample, buttons);
    }

    public static double Score(Layout layout, Transform transform, GridSample sample, bool excludeTags = false)
    {
        if (!transform.IsValid || sample.Width <= 0 || sample.Height <= 0 ||
            sample.Bgra.LongLength != (long)sample.Width * sample.Height * 4) return 0;
        var tags = layout.Cells.Select(c => Geometry.TagBounds(layout, c, transform)).ToArray();
        double? Read(double x, double y)
        {
            var ix = (int)Math.Round(x); var iy = (int)Math.Round(y);
            if (excludeTags && tags.Any(t => ix >= t.X && iy >= t.Y && ix <= t.X + t.Width && iy <= t.Y + t.Height)) return null;
            ix -= sample.X; iy -= sample.Y;
            if (ix < 0 || iy < 0 || ix >= sample.Width || iy >= sample.Height) return null;
            var n = (iy * sample.Width + ix) * 4;
            return (sample.Bgra[n] + 2 * sample.Bgra[n + 1] + sample.Bgra[n + 2]) / 4d;
        }
        var cells = new List<double>();
        foreach (var cell in layout.Cells)
        {
            var origin = Layouts.Origin(layout, cell); double sum = 0;
            foreach (var vertical in new[] { true, false })
            foreach (var side in new[] { false, true })
            {
                double best = 0;
                for (var shift = -1; shift <= 1; shift++)
                {
                    double a = 0, b = 0, c = 0; var reads = 0; var supported = 0;
                    for (var j = 1; j <= 9; j++)
                    {
                        var p = transform.Apply(new(origin.X + (vertical ? (side ? cell.W - GridEvidence.Far(layout) : GridEvidence.Near(layout)) + shift : cell.W * j / 10),
                            origin.Y + (vertical ? cell.H * j / 10 : (side ? cell.H - GridEvidence.Far(layout, false) : GridEvidence.Near(layout, false)) + shift)));
                        var flank = 3 * transform.Scale;
                        var left = Read(p.X - (vertical ? flank : 0), p.Y - (vertical ? 0 : flank));
                        var center = Read(p.X, p.Y);
                        var right = Read(p.X + (vertical ? flank : 0), p.Y + (vertical ? 0 : flank));
                        if (left is null || center is null || right is null) continue;
                        if (GridEvidence.Valley(left.Value, center.Value, right.Value) > 12 && (left.Value + right.Value) / 2 - center.Value >= 6) supported++;
                        a += left.Value; b += center.Value; c += right.Value; reads++;
                    }
                    if (reads >= 5 && supported >= reads - 2) best = Math.Max(best, GridEvidence.Valley(a / reads, b / reads, c / reads));
                }
                sum += best;
            }
            cells.Add(sum / 4);
        }
        return cells.OrderDescending().Take(Math.Max(2, (int)Math.Ceiling(cells.Count * .4))).Average();
    }
}
