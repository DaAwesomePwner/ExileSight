namespace ExileSight.Features.Stashninja;

/// <summary>Retaining a selected bright button tolerates a second bright hover button.</summary>
public static class GridKeepButtons
{
    public static bool Contradicts(string page, Transform transform, GridSample sample, CurrencyButtons buttons)
    {
        var values = new Dictionary<string, double>();
        foreach (var (name, b) in buttons.Buttons)
        {
            var p = transform.Apply(new(b.X, b.Y));
            var left = (int)Math.Ceiling(p.X) - sample.X; var top = (int)Math.Ceiling(p.Y) - sample.Y;
            var right = (int)Math.Floor(p.X + b.Width * transform.Scale) - sample.X;
            var bottom = (int)Math.Floor(p.Y + b.Height * transform.Scale) - sample.Y;
            if (left < 0 || top < 0 || right > sample.Width || bottom > sample.Height || right <= left || bottom <= top) return false;
            double sum = 0; var count = 0;
            for (var y = top; y < bottom; y += 2)
            for (var x = left; x < right; x += 2)
            {
                var n = (y * sample.Width + x) * 4;
                sum += (sample.Bgra[n] + 2 * sample.Bgra[n + 1] + sample.Bgra[n + 2]) / 4d; count++;
            }
            values[name] = sum / count;
        }
        var ranked = values.OrderByDescending(p => p.Value).ToArray();
        // The corpus's +25 hover leads Atlas by 6.1–7.1, but Atlas remains bright
        // relative to the third button. A numerical lead alone misclassifies that hover.
        return ranked[0].Key != page && ranked[0].Value - ranked[1].Value >= CurrencyButtons.MinimumMargin &&
            values[page] - ranked[^1].Value < CurrencyButtons.MinimumMargin;
    }
}
