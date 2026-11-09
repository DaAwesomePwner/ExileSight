namespace ExileSight.Features.Stashninja;

/// <summary>Normalized inner-band evidence, shared by sparse and full reads.</summary>
public static class GridEvidence
{
    // Median strip profiles measured from the owner's local 2000x837 corpus.
    // These are band offsets, not changes to the tag geometry.
    public static double Near(Layout layout, bool vertical = true) => layout.TabId switch { "blight" => 1.5, "delve" => vertical ? 2.5 : 3.5, _ => 3 };
    public static double Far(Layout layout, bool vertical = true) => layout.TabId switch { "delve" => vertical ? 8.5 : 7, "blight" or "essence" => 8.5, _ => 8 };

    public static double Valley(double left, double center, double right)
    {
        var flank = (left + right) / 2;
        var depth = flank - center;
        var supportedDepth = Math.Abs(depth) >= 4 ? depth : 0;
        return Math.Clamp(100 * supportedDepth / (16 + flank), -100, 100);
    }

    internal static float[] Map(float[] luminance, int width, int height, double scale, bool vertical, GridArea area)
    {
        var result = new float[luminance.Length];
        var flank = Math.Max(2, (int)Math.Round(3 * scale));
        var tangent = Math.Max(1, (int)Math.Round(scale));
        var extent = flank + tangent;
        for (var y = Math.Max(extent, area.Y); y < Math.Min(height - extent, area.Y + area.Height); y++)
        for (var x = Math.Max(extent, area.X); x < Math.Min(width - extent, area.X + area.Width); x++)
        {
            double a = 0, b = 0, c = 0;
            for (var t = -1; t <= 1; t++)
            {
                var index = (y + (vertical ? t * tangent : 0)) * width + x + (vertical ? 0 : t * tangent);
                var d = vertical ? flank : flank * width;
                a += luminance[index - d]; c += luminance[index]; b += luminance[index + d];
            }
            // No independent band maxima: both sides must agree on the same
            // cell scale and translation in the joint transform search.
            result[y * width + x] = (float)Valley(a / 3, c / 3, b / 3);
        }
        return result;
    }
}
