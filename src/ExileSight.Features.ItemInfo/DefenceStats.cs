using ExileSight.Core.DataPacks;
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo;

internal static class DefenceStats
{
    internal static readonly string[] Words = ["armour", "evasion", "energy_shield", "ward", "defence", "physical_damage_reduction"];
    internal static readonly (string Property, string Key, string Flat, string Increased)[] Kinds =
    [
        ("Armour", "armour", "local_base_physical_damage_reduction_rating", "local_physical_damage_reduction_rating_+%"),
        ("Evasion Rating", "evasion", "local_base_evasion_rating", "local_evasion_rating_+%"),
        ("Energy Shield", "energy_shield", "local_energy_shield", "local_energy_shield_+%"),
        ("Ward", "ward", "local_ward", "local_ward_+%")
    ];
    internal static readonly Dictionary<string, string[]> Hybrids = new()
    {
        ["local_armour_and_evasion_+%"] = ["armour", "evasion"],
        ["local_armour_and_energy_shield_+%"] = ["armour", "energy_shield"],
        ["local_evasion_and_energy_shield_+%"] = ["evasion", "energy_shield"],
        ["local_armour_and_evasion_and_energy_shield_+%"] = ["armour", "evasion", "energy_shield"]
    };
    private static bool AffectsDefence(string id) => id.StartsWith("local_", StringComparison.Ordinal) &&
        Words.Any(word => id.Contains(word, StringComparison.Ordinal));
    private static bool Supported(string id) => Kinds.Any(k => k.Flat == id || k.Increased == id) ||
        Hybrids.ContainsKey(id) || id is "local_base_ward" or "local_evasion_rating_and_energy_shield";

    internal static StatValue[]? Read(ParsedItem item, IItemInfoData data)
    {
        var result = new List<StatValue>();
        foreach (var line in item.Mods)
        {
            var matches = data.MatchLine(line);
            if (matches.Length == 0)
            {
                // Unknown defence text can conceal an unsupported local modifier; unrelated text is harmless.
                var text = line.Text.Replace(' ', '_').ToLowerInvariant();
                if (Words.Any(text.Contains)) return null;
                continue;
            }
            // On equipment with displayed defences, identical English local/global text resolves locally.
            var local = matches.Select(m => m.Values.Where(v => AffectsDefence(v.StatId)).ToArray())
                .Where(v => v.Length > 0)
                .DistinctBy(v => string.Join(";", v.OrderBy(s => s.StatId).Select(s => $"{s.StatId}:{s.Min}:{s.Max}")))
                .ToArray();
            if (local.Length == 0) continue; // Global and unrelated rounded stats do not change the item property.
            if (local.Length != 1 || local[0].Any(v => v.Min != v.Max || !Supported(v.StatId))) return null;
            result.AddRange(local[0]);
        }
        return result.ToArray();
    }
}
