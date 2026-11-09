using System.Text.RegularExpressions;
using ExileSight.Core.Pricing;

namespace ExileSight.Features.Stashninja;

public sealed record ThresholdProfile(string Name, decimal Minimum, decimal High, string LowColor, string HighColor)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Minimum < 0 || High < Minimum ||
            !Color(LowColor) || !Color(HighColor)) throw new ArgumentException("Invalid threshold profile");
    }
    private static bool Color(string? color) => color is not null && Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$");
}
public sealed record PriceTag(Cell Cell, string Name, decimal Chaos, string Text, string Color, double?[] Sparkline);
public sealed record SearchResult(string? Text, string Status);
public static class Tags
{
    public static PriceTag[] Build(Layout layout, PriceOverview overview, ThresholdProfile profile)
    {
        profile.Validate();
        return layout.Cells.Select(cell => (cell, price: overview.Find(cell.NinjaId ?? cell.ItemName!, cell.Category)))
            .Where(pair => pair.price is not null).Select(pair =>
            {
                var p = pair.price!;
                var divine = overview.ToDivine(p.ChaosValue);
                var text = TagPresentation.Price(p.ChaosValue, divine);
                return new PriceTag(pair.cell, p.Name, p.ChaosValue, text,
                    p.ChaosValue >= profile.High ? profile.HighColor : profile.LowColor, Sparkline(p.History));
            }).ToArray();
    }
    public static double?[] Sparkline(IReadOnlyList<decimal?> changes)
    {
        var values = changes.TakeLast(7).ToArray();
        var result = new double?[7];
        double level = 100;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] is not { } change || change < -100) continue;
            level *= 1 + (double)change / 100;
            result[7 - values.Length + i] = double.IsFinite(level) ? level : null;
        }
        return result;
    }
    // PoE search uses ordinary regex metacharacters; spaces, # and apostrophes stay literal.
    public static string EscapeSearch(string name) => string.Concat(name.Select(c =>
        @"\.^$|?*+()[]{}".Contains(c) ? "\\" + c : c.ToString()));
    public static SearchResult Search(IEnumerable<PriceTag> tags, decimal minimum)
    {
        var names = tags.Where(t => t.Chaos >= minimum).Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length == 0) return new(null, "No priced items meet the threshold");
        if (names.Any(n => string.IsNullOrWhiteSpace(n) || n.Any(char.IsControl) || n.Contains('"')))
            return new(null, "An item name cannot be represented safely in stash search");
        var text = "\"" + string.Join("|", names.Select(EscapeSearch)) + "\"";
        return text.Length <= 250 ? new(text, $"Highlight {names.Length} item types") :
            new(null, "Search exceeds 250 characters — raise the threshold");
    }
}
