namespace ExileSight.Features.Stashninja;

// Authored for ExileSight: local teaching/tolerant matching follows the technique
// of Exile-UI stash-ninja; no reference pixels or game data are ported.
public static class PageSignature
{
    public const int Columns = 48, Rows = 6, Length = Columns * Rows * 3;
    public const double Threshold = .30, Margin = .08;
    public static bool Valid(double[]? values) => values is { Length: Length } && values.All(v => double.IsFinite(v) && Math.Abs(v) <= 32);
    public static double[] Create(int width, int height, byte[] bgra)
    {
        if (width < Columns || height < Rows || (long)width * height > 1_048_576 || bgra.Length != (long)width * height * 4)
            throw new ArgumentException("Invalid sample");
        var result = new double[Length];
        for (var row = 0; row < Rows; row++)
        for (var col = 0; col < Columns; col++)
        {
            var x0 = col * width / Columns; var x1 = (col + 1) * width / Columns;
            var y0 = row * height / Rows; var y1 = (row + 1) * height / Rows;
            for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
            for (var c = 0; c < 3; c++) result[(row * Columns + col) * 3 + c] += bgra[(y * width + x) * 4 + c];
            for (var c = 0; c < 3; c++) result[(row * Columns + col) * 3 + c] /= (x1 - x0) * (y1 - y0);
        }
        for (var c = 0; c < 3; c++)
        {
            var mean = Enumerable.Range(0, Columns * Rows).Average(i => result[i * 3 + c]);
            var std = Math.Sqrt(Enumerable.Range(0, Columns * Rows).Average(i => Math.Pow(result[i * 3 + c] - mean, 2)));
            for (var i = c; i < Length; i += 3) result[i] = (result[i] - mean) / Math.Max(1, std);
        }
        return result;
    }
    public static string? Match(double[] sample, IReadOnlyDictionary<string, double[]> references)
    {
        if (!Valid(sample)) return null;
        var ranked = references.Where(p => Valid(p.Value)).Select(p =>
            (p.Key, Distance: sample.Zip(p.Value).Average(v => Math.Abs(v.First - v.Second))))
            .OrderBy(p => p.Distance).ToArray();
        return ranked.Length > 0 && ranked[0].Distance < Threshold &&
            (ranked.Length == 1 || ranked[1].Distance - ranked[0].Distance >= Margin) ? ranked[0].Key : null;
    }
}
