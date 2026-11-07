using System.Globalization;
using System.Text.RegularExpressions;

namespace ExileSight.Core.Items;

internal static class ModParser
{
    private const string Number = @"[+-]?\d+(?:\.\d+)?";
    private static readonly Regex ValuesPattern = new($@"(?<![\d.])(?<value>{Number})(?:\((?<min>{Number})-(?<max>{Number})\))?", RegexOptions.CultureInvariant);
    internal static decimal? NumberValue(string text) => decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign,
        CultureInfo.InvariantCulture, out var value) ? value : null;
    internal static IReadOnlyList<ModValue> Values(string text) => ValuesPattern.Matches(text.Replace(",", "", StringComparison.Ordinal))
        .Where(m => NumberValue(m.Groups["value"].Value).HasValue)
        .Select(m => new ModValue(NumberValue(m.Groups["value"].Value) ?? 0,
            NumberValue(m.Groups["min"].Value), NumberValue(m.Groups["max"].Value))).ToArray();

    internal static ModLine Parse(string text, string? header, int? group)
    {
        var kind = ModKind.Explicit;
        var headerType = Regex.Replace(header ?? "", "\"[^\"]*\"", "");
        foreach (var (marker, flag) in new (string, ModKind)[] {
            ("implicit", ModKind.Implicit), ("enchantment", ModKind.Enchant), ("crafted", ModKind.Crafted),
            ("fractured", ModKind.Fractured), ("scourge", ModKind.Scourge), ("veiled", ModKind.Veiled),
            ("unique", ModKind.Unique), ("eater of worlds", ModKind.Eater), ("searing exarch", ModKind.Exarch) })
        {
            if (Regex.IsMatch(headerType, @"\b" + marker + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
                text.Contains("(" + (marker == "enchantment" ? "enchant" : marker) + ")", StringComparison.OrdinalIgnoreCase)) kind |= flag;
        }
        if ((kind & (ModKind.Implicit | ModKind.Enchant | ModKind.Scourge)) != 0) kind &= ~ModKind.Explicit;
        var affix = header?.Contains("Prefix", StringComparison.Ordinal) == true ? AffixType.Prefix :
            header?.Contains("Suffix", StringComparison.Ordinal) == true ? AffixType.Suffix : AffixType.Unknown;
        var tier = Regex.Match(header ?? "", @"Tier: (\d+)");
        var name = Regex.Match(header ?? "", "\"([^\"]+)\"");
        return new(text, kind, affix, int.TryParse(tier.Groups[1].Value, out var t) ? t : null,
            name.Success ? name.Groups[1].Value : null, Values(text), group, header);
    }
}
