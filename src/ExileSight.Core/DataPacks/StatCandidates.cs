using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ExileSight.Core.DataPacks;

/// <summary>A necessary literal from each template line rejects unrelated stats before creating regexes.
/// This is only a conservative prefilter: conditions, values and backreferences still use StatText.</summary>
internal static class StatCandidates
{
    private static readonly ConditionalWeakTable<StatVariant, string[]> Anchors = new();

    internal static bool MayMatch(string line, StatVariant variant)
    {
        if (variant.Text.Length > 16384) return false;
        foreach (var anchor in Anchors.GetValue(variant, Build))
            if (line.Contains(anchor, StringComparison.Ordinal)) return true;
        return false;
    }

    private static string[] Build(StatVariant variant) => variant.Text
        .Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line =>
        {
            var longest = "";
            var position = 0;
            foreach (Match slot in StatPattern.Placeholder.Matches(line))
            {
                if (slot.Index - position > longest.Length) longest = line[position..slot.Index];
                position = slot.Index + slot.Length;
            }
            if (line.Length - position > longest.Length) longest = line[position..];
            return longest;
        }).ToArray();
}
