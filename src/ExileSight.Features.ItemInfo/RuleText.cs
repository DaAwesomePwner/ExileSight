using System.Text.RegularExpressions;
namespace ExileSight.Features.ItemInfo;

/// <summary>Converts translation placeholders to representative numbers before existing rule normalization.
/// Multi-line translations create a rule for each copied game line and each selected class.</summary>
public static class RuleText
{
    private static readonly Regex Placeholder = new(@"\{\d+\}|#", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex DisplayNumbers = new(@"[+-]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string Display(string text) => DisplayNumbers.Replace(Placeholder.Replace(text, "#"),
        m => m.Value.StartsWith('+') ? "+#" : m.Value.StartsWith('-') ? "-#" : "#");
    public static string[] Lines(string template) => template.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => Placeholder.Replace(line, "0").Trim()).Where(line => line.Length > 0).Distinct().ToArray();
    public static HighlightRule[] Create(string template, Highlight match, IEnumerable<string>? classes = null,
        bool implicitMod = false, bool enchant = false)
    {
        var selected = classes?.Select(c => c.Trim()).Where(c => c.Length > 0).Select(ItemClasses.Canonicalize).Distinct().ToArray() ?? [];
        string?[] scope = selected.Length == 0 ? new string?[] { null } : selected;
        return Lines(template).SelectMany(line => scope.Select(c => new HighlightRule(line, match, c, implicitMod, enchant))).ToArray();
    }
}
