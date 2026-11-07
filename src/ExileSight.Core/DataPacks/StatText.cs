using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ExileSight.Core.DataPacks;

/// <summary>Pure English stat rendering/matching. Unsupported handlers and unknown ignored values fail closed.</summary>
public static class StatText
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly Regex Placeholder = new(@"\{(\d+)\}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private const string Number = @"[+-]?\d+(?:\.\d+)?";
    private const string Range = @"\(" + Number + @"\s*(?:–|-| to )\s*" + Number + @"\)";
    private static readonly Regex Value = new(@"^(?<sign>[+-]?)(?:\((?<lo>" + Number +
        @")\s*(?:–|-| to )\s*(?<hi>" + Number + @")\)|(?<one>\d+(?:\.\d+)?)(?:" + Range + @")?)$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static string? Render(StatTranslation entry, decimal[] values)
    {
        if (entry.Hidden || values.Length != entry.StatIds.Length) return null;
        var variant = entry.English.FirstOrDefault(v => Valid(v, values.Length) &&
            values.Select((n, i) => v.Conditions[i].Accepts(n)).All(b => b));
        if (variant is null) return null;
        var formatted = new string[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            if (!StatHandlers.Scale(variant.IndexHandlers[i], out var scale)) return null;
            var value = StatHandlers.Apply(values[i], scale);
            formatted[i] = (variant.Formats[i] == "+#" && value > 0 ? "+" : "") + value.ToString(Culture);
        }
        return Placeholder.Replace(variant.Text, m => formatted[int.Parse(m.Groups[1].Value, Culture)]);
    }

    public static StatMatch[] Match(string line, IEnumerable<StatTranslation> entries)
    {
        if (line.Length > 4096) return [];
        var result = new List<StatMatch>();
        foreach (var entry in entries.Where(e => !e.Hidden))
        foreach (var variant in entry.English.Where(v => Valid(v, entry.StatIds.Length)))
        {
            var pattern = new StringBuilder("^");
            var position = 0;
            var seen = new HashSet<int>();
            foreach (Match slot in Placeholder.Matches(variant.Text))
            {
                pattern.Append(Regex.Escape(variant.Text[position..slot.Index]));
                var i = int.Parse(slot.Groups[1].Value, Culture);
                pattern.Append(seen.Add(i) ? $"(?<v{i}>[+-]?(?:{Range}|\\d+(?:\\.\\d+)?(?:{Range})?))" : $@"\k<v{i}>");
                position = slot.Index + slot.Length;
            }
            pattern.Append(Regex.Escape(variant.Text[position..])).Append('$');
            var match = Regex.Match(line, pattern.ToString(), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (!match.Success) continue;
            var values = new List<StatValue>();
            for (var i = 0; i < entry.StatIds.Length; i++)
            {
                if (!StatHandlers.Scale(variant.IndexHandlers[i], out var scale)) break;
                decimal lo, hi;
                if (!seen.Contains(i))
                {
                    // A boolean/ignored stat is recoverable only when its condition fixes its value.
                    var c = variant.Conditions[i];
                    if (c.Negated || c.Min is null || c.Min != c.Max) break;
                    lo = hi = c.Min.Value;
                }
                else
                {
                    var parsed = Value.Match(match.Groups[$"v{i}"].Value);
                    if (!parsed.Success) break;
                    var sign = parsed.Groups["sign"].Value == "-" ? -1 : 1;
                    if (!decimal.TryParse(parsed.Groups["one"].Success ? parsed.Groups["one"].Value : parsed.Groups["lo"].Value,
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Culture, out lo)) break;
                    hi = lo;
                    if (parsed.Groups["hi"].Success && !decimal.TryParse(parsed.Groups["hi"].Value,
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Culture, out hi)) break;
                    try
                    {
                        lo = StatHandlers.Reverse(lo * sign, scale);
                        hi = StatHandlers.Reverse(hi * sign, scale);
                    }
                    catch (OverflowException) { break; }
                }
                if (!variant.Conditions[i].Accepts(lo) || !variant.Conditions[i].Accepts(hi)) break;
                values.Add(new(entry.StatIds[i], Math.Min(lo, hi), Math.Max(lo, hi)));
            }
            if (values.Count == entry.StatIds.Length)
                result.Add(new(entry.Id, values.ToArray()));
        }
        return result.DistinctBy(r => (r.TranslationId, string.Join(";", r.Values.Select(v => $"{v.StatId}:{v.Min}:{v.Max}")))).ToArray();
    }

    private static bool Valid(StatVariant v, int count) =>
        v.Conditions.Length == count && v.Formats.Length == count && v.IndexHandlers.Length == count &&
        v.Formats.All(f => f is "#" or "+#" or "ignored") &&
        Placeholder.Matches(v.Text).All(m => int.TryParse(m.Groups[1].Value, out var i) && i < count);
}
