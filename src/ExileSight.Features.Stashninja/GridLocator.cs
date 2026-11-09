namespace ExileSight.Features.Stashninja;

public sealed record GridSample(int X, int Y, int Width, int Height, byte[] Bgra);
public sealed record GridLocation(Transform? Transform, double Confidence, string Reason, int Evaluations, double Score = 0, int ScorePasses = 0, int ScoreSampleReads = 0);

/// <summary>Display-only, bounded search for inner slot bands. No platform dependencies.</summary>
public static class GridLocator
{
    public const int MaxPixels = 8_388_608;
    public const int MaxEvaluations = 40000;
    public const double MinimumJointScore = 14, MinimumAlignedScore = 12, MinimumCoverage = .65;
    public const double TranslationRange = .03, ScaleRange = .05;
    private readonly record struct Probe(double X, double Y, bool Vertical);
    private readonly record struct Candidate(Transform Transform, double Score);

    public static GridLocation Locate(Layout layout, Resolution client, Transform start, GridSample sample, bool local = false)
    {
        if (!start.IsValid || client.Width <= 0 || client.Height <= 0 || client.Height > 16384 ||
            sample.Width < 8 || sample.Height < 8 || (long)sample.Width * sample.Height > MaxPixels ||
            sample.Bgra.LongLength != (long)sample.Width * sample.Height * 4 || sample.X < 0 || sample.Y < 0 ||
            (long)sample.X + sample.Width > client.Width || (long)sample.Y + sample.Height > client.Height ||
            layout.Cells.Length is < 4 or > 2000)
            return new(null, 0, "Invalid or oversized grid sample", 0);
        var bounds = GridSampling.Bounds(layout, client, start);
        bounds = bounds with { X = bounds.X - sample.X, Y = bounds.Y - sample.Y };
        var luminance = new float[sample.Width * sample.Height];
        var halo = Math.Max(2, (int)Math.Round(3 * start.Scale)) + Math.Max(1, (int)Math.Round(start.Scale));
        for (var y = Math.Max(0, bounds.Y - halo); y < Math.Min(sample.Height, bounds.Y + bounds.Height + halo); y++)
        for (var x = Math.Max(0, bounds.X - halo); x < Math.Min(sample.Width, bounds.X + bounds.Width + halo); x++)
        {
            var i = y * sample.Width + x;
            luminance[i] = (sample.Bgra[i * 4] + 2 * sample.Bgra[i * 4 + 1] + sample.Bgra[i * 4 + 2]) / 4f;
        }
        var probes = new List<Probe>();
        var count = Math.Min(24, layout.Cells.Length);
        for (var i = 0; i < count; i++)
        {
            var cell = layout.Cells[i * layout.Cells.Length / count]; var p = Layouts.Origin(layout, cell);
            for (var j = 1; j <= 3; j++)
            {
                probes.Add(new(p.X + GridEvidence.Near(layout), p.Y + cell.H * j / 4, true));
                probes.Add(new(p.X + cell.W - GridEvidence.Far(layout), p.Y + cell.H * j / 4, true));
                probes.Add(new(p.X + cell.W * j / 4, p.Y + GridEvidence.Near(layout, false), false));
                probes.Add(new(p.X + cell.W * j / 4, p.Y + cell.H - GridEvidence.Far(layout, false), false));
            }
        }
        var points = probes.ToArray(); var evaluations = 0; var scorePasses = 0; var sampleReads = 0;
        var horizontal = GridEvidence.Map(luminance, sample.Width, sample.Height, start.Scale, false, bounds);
        var vertical = GridEvidence.Map(luminance, sample.Width, sample.Height, start.Scale, true, bounds);
        double Read(float[] values, double x, double y)
        {
            sampleReads += 4;
            var ix = (int)x; var iy = (int)y; var fx = x - ix; var fy = y - iy;
            return (values[iy * sample.Width + ix] * (1 - fx) + values[iy * sample.Width + ix + 1] * fx) * (1 - fy) +
                (values[(iy + 1) * sample.Width + ix] * (1 - fx) + values[(iy + 1) * sample.Width + ix + 1] * fx) * fy;
        }
        (double Score, double Coverage)? Score(Transform t, bool confirm = false)
        {
            scorePasses++;
            Span<double> cells = stackalloc double[count]; cells.Clear(); var supported = 0;
            for (var cellIndex = 0; cellIndex < count; cellIndex++)
            for (var axis = 0; axis < 2; axis++)
            {
                double best = double.NegativeInfinity;
                for (var shift = confirm ? 0 : -2; shift <= (confirm ? 0 : 2); shift++)
                {
                    double sum = 0;
                    for (var j = 0; j < 3; j++)
                    for (var side = 0; side < 2; side++)
                    {
                        var p = points[cellIndex * 12 + j * 4 + axis * 2 + side];
                        var x = p.X * t.Scale + t.X - sample.X + (axis == 0 ? shift * t.Scale : 0);
                        var y = p.Y * t.Scale + t.Y - sample.Y + (axis == 1 ? shift * t.Scale : 0);
                        var extent = Math.Max(2, Math.Round(3 * start.Scale)) + Math.Max(1, Math.Round(start.Scale));
                        if (x < extent || y < extent || x >= sample.Width - extent - 1 || y >= sample.Height - extent - 1) { sum = double.NegativeInfinity; break; }
                        sum += Math.Max(-30, Read(axis == 0 ? vertical : horizontal, x, y));
                    }
                    best = Math.Max(best, sum / 6 - 3 * Math.Abs(shift));
                }
                if (!double.IsFinite(best)) return null;
                cells[cellIndex] += best / 2;
            }
            foreach (var cell in cells) if (cell > 6) supported++;
            cells.Sort();
            // Discard the strongest quarter: isolated artwork cannot carry a page.
            var kept = Math.Max(1, count - count / 4);
            double trimmed = 0; for (var i = 0; i < kept; i++) trimmed += cells[i];
            // Four-slot pages constrain scale weakly. Penalize extrapolation past
            // 0.5% from the starting scale so a broad search cannot fit unrelated art.
            var scalePenalty = count < 8 ? 150 * Math.Max(0, Math.Abs(t.Scale / start.Scale - 1) - .005) : 0;
            return (trimmed / kept * Math.Min(1, .6 + count / 20d) - scalePenalty, (double)supported / count);
        }
        var range = local ? 3d : client.Height * TranslationRange;
        var strictBest = new Candidate(start, double.NegativeInfinity);
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
            var strictScore = Score(t, true)?.Score ?? double.NegativeInfinity;
            if (strictScore > strictBest.Score) strictBest = new(t, strictScore);
            if (coarse) { total += score; squared += score * score; coarseCount++; }
            pool.Add(new(t, score));
            if (pool.Count > 16) { pool.Sort((a, b) => b.Score.CompareTo(a.Score)); pool.RemoveRange(8, pool.Count - 8); }
        }
        var steps = local ? 3 : 10;
        for (var s = -steps; s <= steps; s++)
        for (var x = -steps; x <= steps; x++)
        for (var y = -steps; y <= steps; y++)
            Evaluate(new(start.Scale * (1 + s * (local ? .001 : .005)),
                start.X + range * x / steps, start.Y + range * y / steps), true);
        if (coarseCount == 0) return new(null, 0, "Grid lies outside the captured area", evaluations, ScorePasses: scorePasses, ScoreSampleReads: sampleReads);
        var levels = local ? new[] { (Scale: .0002, Shift: .3) } :
            new[] { (Scale: .001, Shift: range / 50), (Scale: .0002, Shift: .15) };
        foreach (var level in levels)
        {
            var seeds = pool.OrderByDescending(c => c.Score).Take(local ? 4 : 8).ToArray();
            var refinement = local ? 1 : 3;
            foreach (var seed in seeds)
            for (var s = -refinement; s <= refinement; s++)
            for (var x = -refinement; x <= refinement; x++)
            for (var y = -refinement; y <= refinement; y++)
                Evaluate(new(seed.Transform.Scale + start.Scale * s * level.Scale,
                    seed.Transform.X + x * level.Shift, seed.Transform.Y + y * level.Shift), false);
        }
        var initialBest = double.IsFinite(strictBest.Score) ? Score(strictBest.Transform) : null;
        if (initialBest is null) return new(null, 0, "Grid lies outside the captured area", evaluations, ScorePasses: scorePasses, ScoreSampleReads: sampleReads);
        var best = new Candidate(strictBest.Transform, initialBest.Value.Score);
        // Final joint fit has no per-cell offset freedom. Four 27-point steps
        // retain the local 559-evaluation cap (343 + 108 + 108).
        foreach (var step in new[] { (Shift: 1d, Scale: .003), (Shift: 1d, Scale: .001), (Shift: .3, Scale: .0002), (Shift: .1, Scale: .0001) })
        {
            var seed = best.Transform;
            var strict = Score(seed, true)?.Score ?? double.NegativeInfinity;
            for (var s = -1; s <= 1; s++)
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            {
                var candidate = new Transform(seed.Scale + start.Scale * s * step.Scale, seed.X + x * step.Shift, seed.Y + y * step.Shift);
                if (evaluations >= MaxEvaluations || Math.Abs(candidate.X - start.X) > range || Math.Abs(candidate.Y - start.Y) > range ||
                    Math.Abs(candidate.Scale / start.Scale - 1) > (local ? .0032 : ScaleRange)) continue;
                evaluations++;
                var score = Score(candidate, true)?.Score ?? double.NegativeInfinity;
                if (score > strict) { strict = score; best = new(candidate, Score(candidate)!.Value.Score); }
            }
        }
        var mean = total / coarseCount; var deviation = Math.Sqrt(Math.Max(1, squared / coarseCount - mean * mean));
        var confidence = (best.Score - mean) / deviation;
        var coherent = GridPrecheck.Score([layout], client, _ => best.Transform, area =>
        {
            if (area.X < sample.X || area.Y < sample.Y || area.X + area.Width > sample.X + sample.Width ||
                area.Y + area.Height > sample.Y + sample.Height) return null;
            var pixels = new byte[area.Width * area.Height * 4];
            for (var row = 0; row < area.Height; row++)
                Buffer.BlockCopy(sample.Bgra, ((area.Y - sample.Y + row) * sample.Width + area.X - sample.X) * 4,
                    pixels, row * area.Width * 4, area.Width * 4);
            return new(area.X, area.Y, area.Width, area.Height, pixels);
        })[layout];
        if (!StashWatchPolicy.ShouldIdentify(coherent))
            return new(null, 0, "Insufficient continuous band contrast", evaluations, ScorePasses: scorePasses, ScoreSampleReads: sampleReads);
        var verified = Score(best.Transform);
        var aligned = Score(best.Transform, true);
        if (verified is null || aligned is null)
            return new(null, 0, "Grid lies outside the captured area", evaluations, ScorePasses: scorePasses, ScoreSampleReads: sampleReads);
        var center = new Point(layout.Cells.Average(c => Geometry.Center(layout, c).X), layout.Cells.Average(c => Geometry.Center(layout, c).Y));
        var displacement = best.Transform.Apply(center); var initialCenter = start.Apply(center);
        if ((local && (Math.Abs(displacement.X - initialCenter.X) >= range - .01 || Math.Abs(displacement.Y - initialCenter.Y) >= range - .01)) || verified.Value.Score < MinimumJointScore || aligned.Value.Score < MinimumAlignedScore || verified.Value.Coverage < (count < 8 ? 1 : MinimumCoverage) || (!local && confidence < 2.5))
            return new(null, confidence, "Grid confidence too low", evaluations, verified?.Score ?? 0, scorePasses, sampleReads);
        // Share the finite, positive uniform-transform consistency rules with manual calibration.
        var a = Geometry.Center(layout, layout.Cells[0]); var b = Geometry.Center(layout, layout.Cells[^1]);
        try { return new(Geometry.Calibrate(a, b, best.Transform.Apply(a), best.Transform.Apply(b)), confidence, "Grid located", evaluations, verified?.Score ?? 0, scorePasses, sampleReads); }
        catch (ArgumentException) { return new(null, confidence, "Inconsistent grid geometry", evaluations, ScorePasses: scorePasses, ScoreSampleReads: sampleReads); }
    }
}
