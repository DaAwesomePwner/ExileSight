// Behaviour reference: Exile-UI modules/stash-ninja.ahk:59–65 (height scaling).
// All geometry and calibration algorithms below are authored for ExileSight.
namespace ExileSight.Features.Stashninja;

public sealed record Transform(double Scale, double X, double Y)
{
    public Point Apply(Point point) => new(point.X * Scale + X, point.Y * Scale + Y);
    public bool IsValid => Layouts.Finite(Scale, X, Y) && Scale > 0 && Scale <= 100;
}
public static class Geometry
{
    public static Point Viewport(Resolution client, double scale)
    {
        if (client.Width <= 0 || client.Height <= 0 || !double.IsFinite(scale) || scale <= 0)
            throw new ArgumentException("Invalid viewport");
        return new(client.Width / scale, client.Height / scale);
    }
    public static Transform Fit(Resolution reference, Resolution client)
    {
        if (reference.Width <= 0 || reference.Height <= 0 || client.Width <= 0 || client.Height <= 0)
            throw new ArgumentException("Resolution must be positive");
        var scale = (double)client.Height / reference.Height;
        var referenceBar = Math.Max(0, (reference.Width - reference.Height * 21d / 9) / 2);
        var clientBar = Math.Max(0, (client.Width - client.Height * 21d / 9) / 2);
        return new(scale, clientBar - referenceBar * scale, 0);
    }
    public static Transform Calibrate(Point a, Point b, Point clickedA, Point clickedB)
    {
        if (!Layouts.Finite(a.X, a.Y, b.X, b.Y, clickedA.X, clickedA.Y, clickedB.X, clickedB.Y))
            throw new ArgumentException("Nonfinite calibration");
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var length = dx * dx + dy * dy;
        if (length < 1) throw new ArgumentException("Choose distinct reference cells");
        var scale = ((clickedB.X - clickedA.X) * dx + (clickedB.Y - clickedA.Y) * dy) / length;
        // Retain the original 4px delta consistency check before sharing residuals.
        var result = new Transform(scale, clickedA.X - a.X * scale, clickedA.Y - a.Y * scale);
        var end = result.Apply(b);
        if (!result.IsValid || Math.Abs(end.X - clickedB.X) > 4 || Math.Abs(end.Y - clickedB.Y) > 4)
            throw new ArgumentException("Clicks must describe a uniform scale and translation");
        return new(scale, (clickedA.X + clickedB.X - (a.X + b.X) * scale) / 2,
            (clickedA.Y + clickedB.Y - (a.Y + b.Y) * scale) / 2);
    }
    public sealed record Bounds(double X, double Y, double Width, double Height);
    public static Bounds TagBounds(Layout layout, Cell cell, Transform transform)
    {
        var center = transform.Apply(Center(layout, cell));
        // Two reference pixels on each edge also separate the measured 40px
        // essence boxes, whose centers are only about 37.9px apart.
        var width = Math.Max(cell.W * .5, cell.W - 4) * transform.Scale;
        var height = Math.Max(cell.H * .5, cell.H - 4) * transform.Scale;
        return new(center.X - width / 2, center.Y - height / 2, width, height);
    }
    public static Point Center(Layout l, Cell c)
    { var p = Layouts.Origin(l, c); return new(p.X + c.W / 2, p.Y + c.H / 2); }
}
