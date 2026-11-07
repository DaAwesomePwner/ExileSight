using System.Text.Json;
using ExileSight.Core.DataPacks;

namespace ExileSight.DataGen;

public static class TierDerivation
{
    // Only item-domain prefixes/suffixes get estimates; implicit/corrupted and other domains remain unranked.
    // Family includes generation type, fine family, groups and ordered stat IDs. First matching weight wins.
    // Descending required level yields dense ranks, ties share rank and are ordered by ordinal mod ID.
    // Identical eligible family ladders are stored once; class/tag contexts reference their stable hash IDs.
    private static string Key(IEnumerable<string> parts) => string.Concat(parts.Select(p =>
        p.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + p));
    public static (TierLadder[] Ladders, TierContext[] Contexts) Derive(BaseEntry[] bases, ModEntry[] mods)
    {
        var ladders = new SortedDictionary<string, TierLadder>(StringComparer.Ordinal);
        var contexts = new List<TierContext>();
        foreach (var context in bases.Where(b => b.Domain == "item")
                     .GroupBy(b => Key(new[] { b.ItemClass }.Concat(b.Tags.Order(StringComparer.Ordinal))))
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var tags = context.First().Tags.ToHashSet(StringComparer.Ordinal);
            tags.Add("default");
            var eligible = mods.Where(m => m.GenerationType is "prefix" or "suffix" && m.Groups.Length > 0 && m.Domain == "item" &&
                m.SpawnWeights.FirstOrDefault(w => tags.Contains(w.Tag)) is { Weight: > 0 });
            var ids = new List<string>();
            foreach (var family in eligible.GroupBy(m => Key(new[] { m.GenerationType, m.Family,
                         Key(m.Groups.Order(StringComparer.Ordinal)), Key(m.Stats.Select(s => s.Id)) })))
            {
                var levels = family.Select(m => m.RequiredLevel).Distinct().OrderDescending().ToArray();
                var ranks = family.OrderBy(m => m.Id, StringComparer.Ordinal)
                    .Select(m => new ModRank(m.Id, Array.IndexOf(levels, m.RequiredLevel) + 1)).ToArray();
                var id = PackCodec.Hash(JsonSerializer.SerializeToUtf8Bytes(ranks, PackJson.Default.ModRankArray));
                ladders.TryAdd(id, new(id, ranks));
                ids.Add(id);
            }
            contexts.Add(new(context.First().ItemClass, context.First().Tags.Order(StringComparer.Ordinal).ToArray(),
                ids.Order(StringComparer.Ordinal).ToArray()));
        }
        return (ladders.Values.ToArray(), contexts.ToArray());
    }
}
