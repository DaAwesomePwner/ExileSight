namespace ExileSight.Features.Stashninja.Tests;

internal static class GridFixture
{
    public static Layout Layout => new("synthetic", "grid", new(1000, 600),
        Enumerable.Range(0, 30).Select(i => new Cell($"c{i}", 90 + i % 6 * 49,
            110 + i / 6 * 53, 34, 38, "Currency", ItemName: "fixture")).ToArray(),
        [], new("synthetic", "measured from screenshot at 1000x600", new(2026, 11, 8)));

    public static Cell[] DistractorCells(Layout layout) =>
        Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"))
            .Where(l => l.TabId == layout.TabId && l.Page != layout.Page)
            .SelectMany(l => Enumerable.Range(0, Math.Min(24, l.Cells.Length))
                .Select(i => l.Cells[i * l.Cells.Length / Math.Min(24, l.Cells.Length)])
                .Where(c => !layout.Cells.Any(own => own.X < c.X + c.W && own.X + own.W > c.X &&
                    own.Y < c.Y + c.H && own.Y + own.H > c.Y)).Take(2)).ToArray();

    public static GridSample Crop(byte[] pixels, Resolution size, GridArea area)
    {
        var crop = new byte[area.Width * area.Height * 4];
        for (var row = 0; row < area.Height; row++)
            Buffer.BlockCopy(pixels, ((area.Y + row) * size.Width + area.X) * 4,
                crop, row * area.Width * 4, area.Width * 4);
        return new(area.X, area.Y, area.Width, area.Height, crop);
    }

    public static byte[] Render(Layout layout, Resolution size, Transform transform, double brightness, double gamma, bool grid = true, bool distractors = false)
    {
        var pixels = new byte[size.Width * size.Height * 4];
        var random = new Random(104);
        var boxes = layout.Cells.Select(c => (P: transform.Apply(Layouts.Origin(layout, c)), W: c.W * transform.Scale, H: c.H * transform.Scale)).ToArray();
        // Unpriced frames at competing sibling probe locations, inside the grid.
        if (distractors && layout.TabId == "currency")
            boxes = boxes.Concat(DistractorCells(layout).Select(c =>
                (P: transform.Apply(new(c.X, c.Y)), W: c.W * transform.Scale, H: c.H * transform.Scale))).ToArray();
        for (var y = 0; y < size.Height; y++)
        for (var x = 0; x < size.Width; x++)
        {
            double value = .42;
            if (grid)
            for (var i = 0; i < boxes.Length; i++)
            {
                var b = boxes[i]; var dx = x - b.P.X; var dy = y - b.P.Y;
                if (dx < -2 || dy < -2 || dx > b.W + 2 || dy > b.H + 2) continue;
                var distance = Math.Min(Math.Min(Math.Abs(dx), Math.Abs(dx - b.W)), Math.Min(Math.Abs(dy), Math.Abs(dy - b.H)));
                value -= .32 * Math.Max(0, 1 - distance / 1.5);
                if (i % 3 == 0 && dx > 7 && dx < b.W - 7 && dy > 7 && dy < b.H - 7) value = .75;
            }
            else value = random.NextDouble();
            var v = (byte)Math.Clamp(255 * brightness * Math.Pow(Math.Max(.01, value), gamma) + random.Next(-7, 8), 0, 255);
            var p = (y * size.Width + x) * 4;
            pixels[p] = pixels[p + 1] = pixels[p + 2] = v; pixels[p + 3] = 255;
        }
        return pixels;
    }
}
