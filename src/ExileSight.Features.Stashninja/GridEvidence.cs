namespace ExileSight.Features.Stashninja;

/// <summary>Normalized inner-band evidence, shared by sparse and full reads.</summary>
public static class GridEvidence
{
    // Median strip profiles measured from the owner's local 2000x837 corpus.
    // These are band offsets, not changes to the tag geometry.
    public static double Near(Layout layout) => layout.TabId switch { "blight" => 1.5, "delve" => 2.5, _ => 3 };
    public static double Far(Layout layout) => layout.TabId is "blight" or "essence" ? 8.5 : 8;

    public static double Valley(double left, double center, double right)
    {
        var flank = Math.Min(left, right);
        return Math.Clamp(100 * (flank - center) / (8 + flank), -100, 100);
    }

    internal static float[] Map(float[] luminance, int width, int height, double scale, bool vertical, GridArea area)
    {
        var result = new float[luminance.Length];
        var flank = Math.Max(2, (int)Math.Round(3 * scale));
        var tangent = Math.Max(1, (int)Math.Round(scale));
        var band = Math.Max(1, (int)Math.Round(2 * scale));
        var extent = flank + band + 2 * tangent;
        for (var y = Math.Max(extent, area.Y); y < Math.Min(height - extent, area.Y + area.Height); y++)
        for (var x = Math.Max(extent, area.X); x < Math.Min(width - extent, area.X + area.Width); x++)
        {
            double best = -100;
            for (var shift = -band; shift <= band; shift++)
            {
                double a = 0, b = 0, c = 0, low = 255, high = 0;
                for (var t = -1; t <= 1; t++)
                {
                    var index = (y + (vertical ? t * tangent : shift)) * width + x + (vertical ? shift : t * tangent);
                    var d = vertical ? flank : flank * width;
                    a += luminance[index - d]; c += luminance[index]; b += luminance[index + d];
                    low = Math.Min(low, luminance[index]); high = Math.Max(high, luminance[index]);
                }
                best = Math.Max(best, Valley(a / 3, c / 3, b / 3) - 10 * (high - low) / (8 + Math.Min(a, b) / 3) - 5 * Math.Abs(shift) / scale);
            }
            result[y * width + x] = (float)best;
        }
        return result;
    }
}
