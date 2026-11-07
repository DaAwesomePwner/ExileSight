using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ExileSight.Core.DataPacks;

internal sealed record StatPattern(Regex Regex, HashSet<int> Seen, bool Partial)
{
    internal const string Number = @"[+-]?\d+(?:\.\d+)?";
    internal const string Range = @"\(" + Number + @"\s*(?:–|-| to )\s*" + Number + @"\)";
    internal static readonly Regex Placeholder = new(@"\{(\d+)\}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly ConditionalWeakTable<StatVariant, Lazy<StatPattern[]>> Cache = new();

    private static readonly object CacheGate = new();
    private static readonly Queue<StatVariant> Recent = new();
    internal const int Capacity = 512;

    // Bound session-wide regex retention even after checking many unrelated item types.
    // Eviction may rebuild a pattern, but never changes pack parsing or matching results.
    internal static StatPattern[] For(StatVariant variant)
    {
        Lazy<StatPattern[]> value;
        lock (CacheGate)
        {
            if (!Cache.TryGetValue(variant, out value!))
            {
                value = new(() => Build(variant));
                Cache.Add(variant, value);
                Recent.Enqueue(variant);
                if (Recent.Count > Capacity) Cache.Remove(Recent.Dequeue());
            }
        }
        return value.Value;
    }
    private static StatPattern[] Build(StatVariant variant)
    {
        var lines = variant.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return lines.Where(l => l.Length > 0).Select(line =>
        {
            var pattern = new StringBuilder(@"\A");
            var position = 0;
            var seen = new HashSet<int>();
            foreach (Match slot in Placeholder.Matches(line))
            {
                pattern.Append(Regex.Escape(line[position..slot.Index]));
                var i = int.Parse(slot.Groups[1].Value, CultureInfo.InvariantCulture);
                pattern.Append(seen.Add(i) ? $"(?<v{i}>[+-]?(?:{Range}|\\d+(?:\\.\\d+)?(?:{Range})?))" : $@"\k<v{i}>");
                position = slot.Index + slot.Length;
            }
            pattern.Append(Regex.Escape(line[position..])).Append(@"\z");
            return new StatPattern(new(pattern.ToString(), RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)), seen, lines.Length > 1);
        }).ToArray();
    }
}
