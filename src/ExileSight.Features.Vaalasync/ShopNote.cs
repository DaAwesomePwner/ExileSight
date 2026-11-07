// Provenance: Exile-UI modules/exchange.ahk:760–860,924–990; omni-key.ahk:54.
// ExileSight adds fractions, both prefixes and symmetric, explicit-click suggestions.
using System.Globalization;
using System.Text.RegularExpressions;

namespace ExileSight.Features.Vaalasync;

public sealed record ShopNote(string Prefix, decimal Amount, string Currency)
{
    public const decimal Maximum = 1_000_000;
    private static readonly Regex Pattern = new(@"^(~b/o|~price) ([0-9]+(?:\.[0-9]+)?(?:/[0-9]+(?:\.[0-9]+)?)?) ([a-z][a-z0-9-]{0,39})$", RegexOptions.CultureInvariant);
    public static ShopNote? Parse(string? text)
    {
        if (text is null || text.Length > 100) return null;
        var match = Pattern.Match(text.Trim());
        if (!match.Success) return null;
        var amount = ParseAmount(match.Groups[2].Value);
        return amount is > 0 and <= Maximum ? new(match.Groups[1].Value, amount.Value, match.Groups[3].Value) : null;
    }
    public static decimal? ParseAmount(string text)
    {
        var parts = text.Split('/');
        if (parts.Length > 2 || !decimal.TryParse(parts[0], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || value <= 0 || value > Maximum) return null;
        if (parts.Length == 2)
        {
            if (!decimal.TryParse(parts[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var divisor)
                || divisor <= 0 || divisor > Maximum) return null;
            if (divisor < value / Maximum) return null;
            value /= divisor;
        }
        return value is > 0 and <= Maximum ? value : null;
    }
    public ShopNote? Suggest(string choice, string custom = "")
    {
        decimal? value = choice switch
        {
            "minus1" => Amount - 1, "plus1" => Amount + 1,
            "minus10" => Round(Amount * .9m), "plus10" => Round(Amount * 1.1m),
            "custom" => ParseAmount(custom), _ => null
        };
        return value is > 0 and <= Maximum ? this with { Amount = value.Value } : null;
    }
    private decimal Round(decimal value) => decimal.Round(value, Currency is "divine" or "mirror" ? 2 : Amount < 10 ? 2 : 0, MidpointRounding.AwayFromZero);
    public override string ToString() => $"{Prefix} {Amount.ToString("0.############################", CultureInfo.InvariantCulture)} {Currency}";
}
