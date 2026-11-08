using ExileSight.Features.Stashninja;
using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class ShippedLayoutGeometryTests
{
    [Fact]
    public void ShippedRowsColumnsAndSizeClassesAreRegular()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        Assert.NotEmpty(layouts);
        foreach (var layout in layouts)
        {
            CheckAxis(layout, "column x", c => Layouts.Origin(layout, c).X + c.W / 2);
            CheckAxis(layout, "row y", c => Layouts.Origin(layout, c).Y + c.H / 2);
            for (var i = 0; i < layout.Cells.Length; i++)
            for (var j = i + 1; j < layout.Cells.Length; j++)
            {
                var a = layout.Cells[i];
                var b = layout.Cells[j];
                if (Math.Max(a.W, b.W) > Math.Min(a.W, b.W) * 1.15 ||
                    Math.Max(a.H, b.H) > Math.Min(a.H, b.H) * 1.15) continue;
                Assert.True(a.W == b.W && a.H == b.H,
                    $"{layout.TabId}/{layout.Page}: size class {a.Id} ({a.W}x{a.H}) / " +
                    $"{b.Id} ({b.W}x{b.H}); spread W={Math.Abs(a.W - b.W)}, " +
                    $"H={Math.Abs(a.H - b.H)} reference px; run normalize.py");
            }
        }
    }

    private static void CheckAxis(Layout layout, string axis, Func<Cell, double> center)
    {
        var sorted = layout.Cells.OrderBy(center).ToArray();
        var start = 0;
        for (var end = 1; end <= sorted.Length; end++)
        {
            // Connected neighbours include noisy rows whose full spread exceeds 3 px.
            if (end < sorted.Length && center(sorted[end]) - center(sorted[end - 1]) <= 3)
                continue;
            var spread = center(sorted[end - 1]) - center(sorted[start]);
            Assert.True(spread <= 0.5,
                $"{layout.TabId}/{layout.Page}: {axis} cells " +
                $"{string.Join(", ", sorted[start..end].Select(c => c.Id))}; " +
                $"spread {spread} reference px exceeds 0.5; run normalize.py");
            start = end;
        }
    }
}
