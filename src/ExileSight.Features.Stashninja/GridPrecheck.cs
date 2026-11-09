namespace ExileSight.Features.Stashninja;

/// <summary>
/// Sparse fixed-transform edge profiles, not a transform search. Averaging along
/// each border tolerates artwork/noise; a ±12px normal band admits first-use offsets.
/// </summary>
public static class GridPrecheck
{
    public const int ProbesPerLayout = 8, PatchSize = 33;
    public const int MaxPixelsPerLayout = ProbesPerLayout * PatchSize * PatchSize;

    public static Dictionary<Layout, double> Score(IReadOnlyList<Layout> layouts, Resolution size,
        Func<Layout, Transform?> calibration, Func<GridArea, GridSample?> capture)
    {
        var scores = new Dictionary<Layout, double>();
        foreach (var layout in layouts)
        {
            var start = calibration(layout) ?? Geometry.Fit(layout.ReferenceResolution, size);
            double total = 0; var count = Math.Min(4, layout.Cells.Length);
            for (var i = 0; i < count; i++)
            {
                var cell = layout.Cells[i * layout.Cells.Length / count];
                var origin = Layouts.Origin(layout, cell);
                foreach (var vertical in new[] { true, false })
                {
                    var p = start.Apply(new(origin.X + (vertical ? GridEvidence.Near(layout) : cell.W / 2),
                        origin.Y + (vertical ? cell.H / 2 : GridEvidence.Near(layout, false))));
                    var area = new GridArea((int)Math.Round(p.X) - 16, (int)Math.Round(p.Y) - 16, PatchSize, PatchSize);
                    if (area.X < 0 || area.Y < 0 || area.X + PatchSize > size.Width || area.Y + PatchSize > size.Height) continue;
                    var image = capture(area);
                    if (image is null || image.Width != PatchSize || image.Height != PatchSize ||
                        image.Bgra.Length != PatchSize * PatchSize * 4) continue;
                    double Read(int normal, int tangent)
                    {
                        var x = vertical ? normal : tangent; var y = vertical ? tangent : normal;
                        var index = (y * PatchSize + x) * 4;
                        return (image.Bgra[index] + 2 * image.Bgra[index + 1] + image.Bgra[index + 2]) / 4d;
                    }
                    double best = 0;
                    var flank = Math.Clamp((int)Math.Round(3 * start.Scale), 3, 8);
                    for (var n = Math.Max(4, flank); n <= Math.Min(28, 32 - flank); n++)
                    {
                        double left = 0, center = 0, right = 0; var supported = 0;
                        for (var t = 7; t <= 25; t++)
                        {
                            var a = Read(n - flank, t); var b = Read(n, t); var c = Read(n + flank, t);
                            var contrast = GridEvidence.Valley(a, b, c);
                            left += a; center += b; right += c;
                            if (contrast > 12 && (a + c) / 2 - b >= 6) supported++;
                        }
                        if (supported >= 18)
                        {
                            var flankLevel = (left + right) / 38;
                            var darkSupport = Math.Clamp((flankLevel - 8) / 20, 0, 1);
                            best = Math.Max(best, darkSupport * GridEvidence.Valley(left / 19, center / 19, right / 19));
                        }
                    }
                    total += best;
                }
            }
            scores[layout] = total / (count * 2);
        }
        return scores;
    }
}
