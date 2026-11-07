using System.Text.RegularExpressions;
using ExileSight.Core.DataPacks;

namespace ExileSight.Features.ItemInfo;

// Exile-UI item-checker rule identity: ignore rolled numbers, ranges and copy annotations.
public static class ModRuleKey
{
    private static readonly Regex Numbers = new(@"\([+-]?\d+(?:\.\d+)?\s*(?:-|–| to )\s*[+-]?\d+(?:\.\d+)?\)|[+-]?\d+(?:\.\d+)?",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string Normalize(string text) =>
        Whitespace.Replace(Numbers.Replace(StatText.StripCopySuffixes(text.ToLowerInvariant()), ""), " ").Trim().ToUpperInvariant();
}
