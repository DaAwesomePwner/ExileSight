namespace ExileSight.Features.Stashninja;

public sealed record GridSample(int X, int Y, int Width, int Height, byte[] Bgra);
public sealed record GridLocation(Transform? Transform, double Confidence, string Reason, int Evaluations);

/// <summary>Display-only, bounded search for dark slot frames. No platform dependencies.</summary>
public static class GridLocator
{
    public const int MaxPixels = 8_388_608;
    public const int MaxEvaluations = 40000;
    public const double TranslationRange = .03, ScaleRange = .05;
    private readonly record struct Probe(double X, double Y, bool Vertical);
    private readonly record struct Candidate(Transform Transform, double Score);

    public static GridLocation Locate(Layout layout, Resolution client, Transform start, GridSample sample)
    {
        if (!start.IsValid || client.Width <= 0 || client.Height <= 0 || client.Height > 16384 ||
            sample.Width < 8 || sample.Height < 8 || (long)sample.Width * sample.Height > MaxPixels ||
            sample.Bgra.LongLength != (long)sample.Width * sample.Height * 4 || sample.X < 0 || sample.Y < 0 ||
            (long)sample.X + sample.Width > client.Width || (long)sample.Y + sample.Height > client.Height ||
            layout.Cells.Length is < 4 or > 2000)
            return new(null, 0, "Invalid or oversized grid sample", 0);
        var luminance = new float[sample.Width * sample.Height];
        for (var i = 0; i < luminance.Length; i++)
            luminance[i] = (sample.Bgra[i * 4] + 2 * sample.Bgra[i * 4 + 1] + sample.Bgra[i * 4 + 2]) / 4f;
        var probes = new List<Probe>();
        var count = Math.Min(24, layout.Cells.Length);
        for (var i = 0; i < count; i++)
        {
            var cell = layout.Cells[i * layout.Cells.Length / count]; var p = Layouts.Origin(layout, cell);
            for (var j = 1; j <= 3; j++)
            {
                probes.Add(new(p.X, p.Y + cell.H * j / 4, true));
                probes.Add(new(p.X + cell.W, p.Y + cell.H * j / 4, true));
                probes.Add(new(p.X + cell.W * j / 4, p.Y, false));
                probes.Add(new(p.X + cell.W * j / 4, p.Y + cell.H, false));
            }
        }
        var points = probes.ToArray(); var evaluations = 0;
        double Read(double x, double y)
        {
            var ix = (int)x; var iy = (int)y; var fx = x - ix; var fy = y - iy;
            return (luminance[iy * sample.Width + ix] * (1 - fx) + luminance[iy * sample.Width + ix + 1] * fx) * (1 - fy) +
                (luminance[(iy + 1) * sample.Width + ix] * (1 - fx) + luminance[(iy + 1) * sample.Width + ix + 1] * fx) * fy;
        }
        (double Score, double Coverage)? Score(Transform t)
        {
            double sum = 0; var supported = 0;
            foreach (var p in points)
            {
                var x = p.X * t.Scale + t.X - sample.X; var y = p.Y * t.Scale + t.Y - sample.Y;
                var d = Math.Max(3, 2 * t.Scale);
                if (x < d || y < d || x >= sample.Width - d - 1 || y >= sample.Height - d - 1) return null;
                // Dark frame against its two neighbours: interior artwork contributes little
                // because probes stay close to the border and avoid corners.
                var contrast = (p.Vertical ? Read(x - d, y) + Read(x + d, y) : Read(x, y - d) + Read(x, y + d)) / 2 - Read(x, y);
                sum += Math.Clamp(contrast, -80, 80);
                if (contrast > 6) supported++;
            }
            return (sum / points.Length, (double)supported / points.Length);
        }
        var range = client.Height * TranslationRange;
        var pool = new List<Candidate>(); double total = 0, squared = 0; var coarseCount = 0;
        void Evaluate(Transform t, bool coarse)
        {
            if (evaluations >= MaxEvaluations || !t.IsValid || Math.Abs(t.X - start.X) > range + .001 ||
                Math.Abs(t.Y - start.Y) > range + .001 || Math.Abs(t.Scale / start.Scale - 1) > ScaleRange + .000001) return;
            var measured = Score(t); evaluations++;
            // Missing image support is not a low score: exclude it from both the
            // candidate pool and the confidence distribution.
            if (measured is null) return;
            var score = measured.Value.Score;
            if (coarse) { total += score; squared += score * score; coarseCount++; }
            pool.Add(new(t, score));
            if (pool.Count > 16) { pool.Sort((a, b) => b.Score.CompareTo(a.Score)); pool.RemoveRange(8, pool.Count - 8); }
        }
        // 21 scales x 21 x 21 translations; refinement is capped at 8 seeds per level.
        for (var s = -10; s <= 10; s++)
        for (var x = -10; x <= 10; x++)
        for (var y = -10; y <= 10; y++)
            Evaluate(new(start.Scale * (1 + s * .005), start.X + range * x / 10, start.Y + range * y / 10), true);
        if (coarseCount == 0) return new(null, 0, "Grid lies outside the captured area", evaluations);
        foreach (var level in new[] { (Scale: .001, Shift: range / 50), (Scale: .0002, Shift: .15) })
        {
            var seeds = pool.OrderByDescending(c => c.Score).Take(8).ToArray();
            foreach (var seed in seeds)
            for (var s = -3; s <= 3; s++)
            for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
                Evaluate(new(seed.Transform.Scale + start.Scale * s * level.Scale,
                    seed.Transform.X + x * level.Shift, seed.Transform.Y + y * level.Shift), false);
        }
        var best = pool.MaxBy(c => c.Score);
        var mean = total / coarseCount; var deviation = Math.Sqrt(Math.Max(1, squared / coarseCount - mean * mean));
        var confidence = (best.Score - mean) / deviation;
        if (best.Score < 8 || Score(best.Transform)!.Value.Coverage < .75 || confidence < 2.5)
            return new(null, confidence, "Grid confidence too low", evaluations);
        // Share the finite, positive uniform-transform consistency rules with manual calibration.
        var a = Geometry.Center(layout, layout.Cells[0]); var b = Geometry.Center(layout, layout.Cells[^1]);
        try { return new(Geometry.Calibrate(a, b, best.Transform.Apply(a), best.Transform.Apply(b)), confidence, "Grid located", evaluations); }
        catch (ArgumentException) { return new(null, confidence, "Inconsistent grid geometry", evaluations); }
    }
}
