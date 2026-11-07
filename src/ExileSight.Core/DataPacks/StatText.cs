using System.Globalization;
using System.Text.RegularExpressions;

namespace ExileSight.Core.DataPacks;

/// <summary>Pure English stat rendering/matching. Treat loaded translations as immutable.</summary>
public static class StatText
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly Regex Value = new(@"^(?<sign>[+-]?)(?:\((?<lo>" + StatPattern.Number +
        @")\s*(?:–|-| to )\s*(?<hi>" + StatPattern.Number + @")\)|(?<one>\d+(?:\.\d+)?)(?:" + StatPattern.Range + @")?)$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly string[] Suffixes = ["(crafted)", "(implicit)", "(fractured)", "(enchant)", "— Unscalable Value"];

    /// <summary>Call before Match for advanced-copy annotations; unknown suffixes remain literal.</summary>
    public static string StripCopySuffixes(string line)
    {
        line = line.TrimEnd();
        while (Suffixes.FirstOrDefault(s => line.EndsWith(" " + s, StringComparison.Ordinal)) is string suffix)
            line = line[..^suffix.Length].TrimEnd();
        return line;
    }

    /// <summary>Optional eager pattern preparation; matching otherwise caches patterns lazily.</summary>
    public static void Prepare(IEnumerable<StatTranslation> entries)
    {
        foreach (var entry in entries.Where(e => !e.Hidden))
        foreach (var variant in entry.English.Where(v => Valid(v, entry.StatIds.Length)))
            _ = StatPattern.For(variant);
    }

    /// <summary>Optional background pattern preparation for callers that need eager warmup.</summary>
    public static Task PrepareAsync(IEnumerable<StatTranslation> entries, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Prepare([entry]);
            }
        }, cancellationToken);

    public static string? Render(StatTranslation entry, decimal[] values)
    {
        if (entry.Hidden || values.Length != entry.StatIds.Length) return null;
        var variant = entry.English.FirstOrDefault(v => Valid(v, values.Length) &&
            values.Select((n, i) => v.Conditions[i].Accepts(n)).All(b => b));
        if (variant is null) return null;
        try
        {
            var formatted = new string[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var transform = StatHandlers.Resolve(variant.IndexHandlers[i]);
                if (transform is null) return null;
                var value = transform.Apply(values[i]);
                formatted[i] = (variant.Formats[i] == "+#" && value > 0 ? "+" : "") + value.ToString("0.############################", Culture);
            }
            return StatPattern.Placeholder.Replace(variant.Text, m => formatted[int.Parse(m.Groups[1].Value, Culture)]);
        }
        catch (Exception ex) when (ex is OverflowException or RegexMatchTimeoutException) { return null; }
    }

    public static StatMatch[] Match(string line, IEnumerable<StatTranslation> entries)
    {
        if (line.Length > 4096) return [];
        var result = new List<StatMatch>();
        foreach (var entry in entries.Where(e => !e.Hidden))
        foreach (var variant in entry.English.Where(v => StatCandidates.MayMatch(line, v) && Valid(v, entry.StatIds.Length)))
        {
            try
            {
                foreach (var pattern in StatPattern.For(variant))
                {
                    var match = pattern.Regex.Match(line);
                    if (!match.Success) continue;
                    var values = ReadValues(entry, variant, pattern, match);
                    if (values is { Length: > 0 }) result.Add(new(entry.Id, values));
                }
            }
            catch (RegexMatchTimeoutException) { /* Untrusted copied text: no candidate on timeout. */ }
        }
        return result.DistinctBy(r => (r.TranslationId, string.Join(";", r.Values.Select(v => $"{v.StatId}:{v.Min}:{v.Max}")))).ToArray();
    }

    private static StatValue[]? ReadValues(StatTranslation entry, StatVariant variant, StatPattern pattern, Match match)
    {
        var values = new List<StatValue>();
        for (var i = 0; i < entry.StatIds.Length; i++)
        {
            decimal lo, hi;
            if (!pattern.Seen.Contains(i))
            {
                // A separate copied line cannot establish stats or conditions on the other lines.
                if (pattern.Partial) continue;
                var c = variant.Conditions[i];
                if (c.Negated || c.Min is null || c.Min != c.Max) return null;
                lo = hi = c.Min.Value;
            }
            else
            {
                var transform = StatHandlers.Resolve(variant.IndexHandlers[i]);
                if (transform is null) return null;
                var parsed = Value.Match(match.Groups[$"v{i}"].Value);
                if (!parsed.Success) return null;
                var sign = parsed.Groups["sign"].Value == "-" ? -1 : 1;
                if (!decimal.TryParse(parsed.Groups["one"].Success ? parsed.Groups["one"].Value : parsed.Groups["lo"].Value,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Culture, out lo)) return null;
                hi = lo;
                if (parsed.Groups["hi"].Success && !decimal.TryParse(parsed.Groups["hi"].Value,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Culture, out hi)) return null;
                if (!transform.Reverse(lo * sign, out var a, out var b) ||
                    !transform.Reverse(hi * sign, out var c, out var d)) return null;
                lo = Math.Min(a, c);
                hi = Math.Max(b, d);
            }
            var condition = variant.Conditions[i];
            if (!condition.Negated)
            {
                lo = Math.Max(lo, condition.Min ?? lo);
                hi = Math.Min(hi, condition.Max ?? hi);
            }
            // A negated condition that creates disjoint intervals cannot be represented by one range.
            if (lo > hi || !condition.Accepts(lo) || !condition.Accepts(hi) ||
                (condition.Negated && condition.Min >= lo && condition.Max <= hi)) return null;
            values.Add(new(entry.StatIds[i], lo, hi));
        }
        return values.ToArray();
    }

    private static bool Valid(StatVariant v, int count) =>
        v.Text.Length <= 16384 && v.Conditions.Length == count && v.Formats.Length == count && v.IndexHandlers.Length == count &&
        v.Formats.All(f => f is "#" or "+#" or "ignored") &&
        StatPattern.Placeholder.Matches(v.Text).All(m => int.TryParse(m.Groups[1].Value, out var i) && i < count);
}
