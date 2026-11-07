using ExileSight.Core.DataPacks;

namespace ExileSight.DataGen;

public static class TierDerivation
{
    // A tier is a level-order estimate, not a claim about power or official in-game tiers.
    // Context = item class + complete sorted base tag set. First matching spawn weight wins.
    // Family = domain + generation type + groups + ordered stat ids; only ordinary prefixes/suffixes.
    // Highest required level is tier 1; equal levels share a dense rank. Never parse an id suffix.
    public static ModEntry[] Derive(BaseEntry[] bases, ModEntry[] mods)
    {
        var tiers = mods.ToDictionary(m => m.Id, _ => new List<ModTier>());
        foreach (var context in bases.GroupBy(b => (b.ItemClass, Tags: string.Join(",", b.Tags.Order(StringComparer.Ordinal))))
                     .OrderBy(g => g.Key.ItemClass, StringComparer.Ordinal).ThenBy(g => g.Key.Tags, StringComparer.Ordinal))
        {
            var domain = context.First().Domain;
            var tags = context.First().Tags.ToHashSet(StringComparer.Ordinal);
            tags.Add("default");
            var eligible = mods.Where(m => m.GenerationType is "prefix" or "suffix" && m.Groups.Length > 0 && m.Domain == domain &&
                m.SpawnWeights.FirstOrDefault(w => tags.Contains(w.Tag)) is { Weight: > 0 });
            foreach (var family in eligible.GroupBy(m => string.Join("|", m.Domain, m.GenerationType, m.Family,
                         string.Join(",", m.Groups.Order(StringComparer.Ordinal)), string.Join(",", m.Stats.Select(s => s.Id)))))
            {
                var levels = family.Select(m => m.RequiredLevel).Distinct().OrderDescending().ToArray();
                foreach (var mod in family)
                    tiers[mod.Id].Add(new(context.Key.ItemClass, context.Key.Tags, Array.IndexOf(levels, mod.RequiredLevel) + 1));
            }
        }
        return mods.Select(m => m with { Tiers = tiers[m.Id].ToArray() }).ToArray();
    }
}
