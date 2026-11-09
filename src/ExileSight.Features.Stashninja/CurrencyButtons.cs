using System.Text.Json;

namespace ExileSight.Features.Stashninja;

/// <summary>Display-only sibling selection; never supplies a grid identity by itself.</summary>
public sealed record CurrencyButtons(Resolution ReferenceResolution, Dictionary<string, Geometry.Bounds> Buttons)
{
    public const double MinimumMargin = 6;
    public static CurrencyButtons Load(string path)
    {
        if (new FileInfo(path).Length > 16384) throw new JsonException("Recognition data too large");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("referenceResolution", out var resolution) || !root.TryGetProperty("subTabs", out var subTabs))
            throw new JsonException("Missing currency button data");
        var result = new CurrencyButtons(resolution.Deserialize<Resolution>(Layouts.Json) ?? throw new JsonException(),
            (subTabs.Deserialize<Dictionary<string, Geometry.Bounds>>(Layouts.Json) ?? throw new JsonException())
                .Where(p => p.Key.StartsWith("currency/", StringComparison.Ordinal)).ToDictionary(p => p.Key[9..], p => p.Value));
        if (result.ReferenceResolution is not { Width: > 0, Height: > 0 } || result.Buttons.Count != 3 || !new[] { "general", "atlas", "league" }.All(result.Buttons.ContainsKey) ||
            result.Buttons.Values.Any(b => !Layouts.Finite(b.X, b.Y, b.Width, b.Height) || b.X < 0 || b.Y < 0 ||
                b.Width <= 0 || b.Height <= 0 || b.X + b.Width > result.ReferenceResolution.Width || b.Y + b.Height > result.ReferenceResolution.Height))
            throw new JsonException("Invalid currency buttons");
        return result;
    }

    public (string? Page, double Margin) Identify(Resolution size, GridSample sample, Transform? transform = null)
    {
        if (sample.Bgra.LongLength != (long)sample.Width * sample.Height * 4) return (null, 0);
        var fit = transform ?? Geometry.Fit(ReferenceResolution, size);
        var scores = new List<(string Page, double Score)>();
        foreach (var (page, b) in Buttons)
        {
            var p = fit.Apply(new(b.X, b.Y));
            var left = (int)Math.Ceiling(p.X) - sample.X; var top = (int)Math.Ceiling(p.Y) - sample.Y;
            var right = (int)Math.Floor(p.X + b.Width * fit.Scale) - sample.X;
            var bottom = (int)Math.Floor(p.Y + b.Height * fit.Scale) - sample.Y;
            if (left < 0 || top < 0 || right > sample.Width || bottom > sample.Height || right <= left || bottom <= top) return (null, 0);
            double sum = 0; var count = 0;
            for (var y = top; y < bottom; y += 2)
            for (var x = left; x < right; x += 2)
            {
                var i = (y * sample.Width + x) * 4;
                sum += (sample.Bgra[i] + 2 * sample.Bgra[i + 1] + sample.Bgra[i + 2]) / 4d; count++;
            }
            scores.Add((page, sum / count));
        }
        scores.Sort((a, b) => b.Score.CompareTo(a.Score));
        var margin = scores[0].Score - scores[1].Score;
        return (margin >= MinimumMargin ? scores[0].Page : null, margin);
    }
}
