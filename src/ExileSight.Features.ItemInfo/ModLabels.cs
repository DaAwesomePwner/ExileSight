using System.Collections.Frozen;
using ExileSight.Core.DataPacks;

namespace ExileSight.Features.ItemInfo;

/// <summary>ExileSight-authored labels for the compact item card, keyed by RePoE stat ID.</summary>
public static class ModLabels
{
    public const int MaximumLength = 14;

    /// <summary>Immutable, ordinal lookup; labels are presentation wording, not patch-dependent values.</summary>
    public static FrozenDictionary<string, string> Entries { get; } = ModLabelTable.Create();

    /// <summary>Exact ordered two-stat combinations; extra or reversed IDs do not match.</summary>
    public static FrozenDictionary<(string First, string Second), string> CombinedEntries { get; } =
        new Dictionary<(string First, string Second), string>
        {
            [("local_physical_damage_+%", "local_accuracy_rating")] = "% phys + acc"
        }.ToFrozenDictionary();

    /// <summary>
    /// Uses an exact ordered combination when available, otherwise the first stat in caller-supplied
    /// (pack) order for hybrid mods. An unknown first stat,
    /// or no stats, uses the template fallback even if a later stat is known. Known lookups allocate nothing.
    /// Pass matched StatValue.StatId values, not a translation ID or a mod ID.
    /// Supply the stat template or line text with copy suffixes such as (crafted) / (implicit)
    /// stripped. The fallback also strips recognized copy suffixes defensively.
    /// </summary>
    public static string For(IReadOnlyList<string> statIds, string templateText)
    {
        ArgumentNullException.ThrowIfNull(statIds);
        ArgumentNullException.ThrowIfNull(templateText);
        if (statIds.Count == 2 && CombinedEntries.TryGetValue((statIds[0], statIds[1]), out var combined))
            return combined;
        return statIds.Count > 0 && Entries.TryGetValue(statIds[0], out var label)
            ? label : Fallback(StatText.StripCopySuffixes(templateText));
    }

    // Keep template letters and their casing; numbers, placeholders, signs, units such as %,
    // brackets and punctuation become word separators. Collapse separators and trim both ends.
    // Retain at most 13 characters plus one ellipsis (14 total); trim before the ellipsis.
    // Empty/numeric-only templates are explicitly unknown. A bounded stack buffer avoids
    // allocating a cleaned copy of arbitrarily long templates.
    private static string Fallback(string template)
    {
        Span<char> text = stackalloc char[MaximumLength + 1];
        var count = 0;
        var separator = false;
        foreach (var c in template)
        {
            if (!char.IsLetter(c))
            {
                separator = count > 0;
                continue;
            }
            if (separator)
            {
                text[count++] = ' ';
                separator = false;
                if (count == text.Length) break;
            }
            text[count++] = c;
            if (count == text.Length) break;
        }
        if (count == 0) return "Unknown";
        if (count <= MaximumLength) return new string(text[..count]);
        var end = MaximumLength - 1;
        while (end > 0 && text[end - 1] == ' ') end--;
        text[end] = '…';
        return new string(text[..(end + 1)]);
    }
}
