// Provenance: Exile-UI modules/item-checker.ahk:2354–2613 and 865–1100 (MIT).
// Copied properties already contain local modifiers; never apply those modifiers a second time.
using System.Globalization;
using System.Text.RegularExpressions;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Items;

namespace ExileSight.Features.GearCmp;

public sealed record EquippedGear(string ItemClass, Dictionary<string, decimal> Stats, string[]? UnknownStats = null);
public static class GearStats
{
    // Verified against generated poe1/stats at RePoE d0bc438357d849bb93ad22f3db0b12938aaf6da1.
    private static readonly Dictionary<string, string[]> Known = new()
    {
        ["fire_and_chaos_damage_resistance_%"] = ["Fire resistance", "Chaos resistance"],
        ["cold_and_chaos_damage_resistance_%"] = ["Cold resistance", "Chaos resistance"],
        ["lightning_and_chaos_damage_resistance_%"] = ["Lightning resistance", "Chaos resistance"],
        ["fire_and_cold_damage_resistance_%"] = ["Fire resistance", "Cold resistance"],
        ["fire_and_lightning_damage_resistance_%"] = ["Fire resistance", "Lightning resistance"],
        ["cold_and_lightning_damage_resistance_%"] = ["Cold resistance", "Lightning resistance"],
        ["additional_strength_and_dexterity"] = ["Strength", "Dexterity"],
        ["additional_strength_and_intelligence"] = ["Strength", "Intelligence"],
        ["additional_dexterity_and_intelligence"] = ["Dexterity", "Intelligence"],
        ["base_maximum_life"] = ["Life"], ["base_maximum_mana"] = ["Mana"],
        ["base_maximum_energy_shield"] = ["Global energy shield"],
        ["additional_strength"] = ["Strength"], ["additional_dexterity"] = ["Dexterity"],
        ["additional_intelligence"] = ["Intelligence"], ["additional_all_attributes"] = ["Strength", "Dexterity", "Intelligence"],
        ["base_strength"] = ["Strength"], ["base_dexterity"] = ["Dexterity"],
        ["base_intelligence"] = ["Intelligence"], ["base_all_attributes"] = ["Strength", "Dexterity", "Intelligence"],
        ["base_fire_damage_resistance_%"] = ["Fire resistance"],
        ["base_cold_damage_resistance_%"] = ["Cold resistance"],
        ["base_lightning_damage_resistance_%"] = ["Lightning resistance"],
        ["base_chaos_damage_resistance_%"] = ["Chaos resistance"],
        ["base_resist_all_elements_%"] = ["Fire resistance", "Cold resistance", "Lightning resistance"]
    };
    public static EquippedGear Read(ParsedItem item, Func<ModLine, StatMatch[]> match)
    {
        var stats = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var uncertain = new HashSet<string>();
        if (item.Unidentified) uncertain.UnionWith(Known.Values.SelectMany(v => v));
        foreach (var line in item.Mods)
        {
            var candidates = match(line);
            if (candidates.Length == 0) uncertain.UnionWith(Known.Values.SelectMany(v => v));
            var signatures = candidates.Select(c => c.Values)
                .DistinctBy(v => string.Join(";", v.OrderBy(x => x.StatId).Select(x => $"{x.StatId}:{x.Min}:{x.Max}"))).ToArray();
            if (signatures.Length != 1)
            {
                foreach (var value in candidates.SelectMany(c => c.Values))
                    uncertain.UnionWith(Affected(value.StatId));
                continue;
            }
            foreach (var value in signatures[0])
            {
                if (!Known.TryGetValue(value.StatId, out var names))
                { uncertain.UnionWith(Affected(value.StatId)); continue; }
                if (value.Min != value.Max) { uncertain.UnionWith(names); continue; }
                foreach (var name in names) stats[name] = stats.GetValueOrDefault(name) + value.Min;
            }
        }
        foreach (var name in uncertain) stats.Remove(name);
        foreach (var name in new[] { "Armour", "Evasion Rating", "Energy Shield", "Ward" })
        {
            if (Property(item, name) is { Length: 1 } values) stats[name] = values[0];
            else if (item.Properties.ContainsKey(name)) uncertain.Add(name);
        }
        if (Property(item, "Attacks per Second") is { Length: 1 } speed)
        {
            foreach (var (property, label) in new[] { ("Physical Damage", "Physical DPS"), ("Elemental Damage", "Elemental DPS"), ("Chaos Damage", "Chaos DPS") })
            {
                var values = Property(item, property);
                if (values is null || values.Length == 0 || values.Length % 2 != 0)
                {
                    if (item.Properties.ContainsKey(property)) uncertain.Add(label);
                    continue;
                }
                stats[label] = values.Sum() / 2 * speed[0];
            }
        }
        else if (item.Properties.ContainsKey("Physical Damage") || item.Properties.ContainsKey("Elemental Damage") || item.Properties.ContainsKey("Chaos Damage"))
            uncertain.UnionWith(["Physical DPS", "Elemental DPS", "Chaos DPS"]);
        return new(item.ItemClass, stats, uncertain.ToArray());
    }
    // Exact IDs only: leech, regeneration and other unrelated mechanics do not change flat totals.
    private static readonly Dictionary<string, string[]> UnsupportedTracked = new()
    {
        ["maximum_life_+%"] = ["Life"], ["maximum_mana_+%"] = ["Mana"],
        ["maximum_energy_shield_+%"] = ["Global energy shield"],
        ["strength_+%"] = ["Strength"], ["dexterity_+%"] = ["Dexterity"], ["intelligence_+%"] = ["Intelligence"],
        ["all_attributes_+%"] = ["Strength", "Dexterity", "Intelligence"],
        ["base_maximum_fire_damage_resistance_%"] = ["Fire resistance"],
        ["base_maximum_cold_damage_resistance_%"] = ["Cold resistance"],
        ["base_maximum_lightning_damage_resistance_%"] = ["Lightning resistance"],
        ["base_maximum_chaos_damage_resistance_%"] = ["Chaos resistance"],
        ["all_elemental_resistances_while_moving"] = ["Fire resistance", "Cold resistance", "Lightning resistance"],
        ["all_resistances_while_moving"] = ["Fire resistance", "Cold resistance", "Lightning resistance", "Chaos resistance"]
    };
    private static IEnumerable<string> Affected(string id) => Known.TryGetValue(id, out var names)
        ? names : UnsupportedTracked.GetValueOrDefault(id) ?? [];
    private static decimal[]? Property(ParsedItem item, string name)
    {
        if (!item.Properties.TryGetValue(name, out var property)) return null;
        var text = property.Text.Replace(" (augmented)", "", StringComparison.Ordinal);
        // Some parser versions retain the property label; accept only the value portion.
        if (text.Contains(':')) text = text[(text.IndexOf(':') + 1)..].Trim();
        if (!Regex.IsMatch(text, @"^\d+(?:\.\d+)?(?:-\d+(?:\.\d+)?)*(?:, \d+(?:\.\d+)?-\d+(?:\.\d+)?)*$",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return null;
        return Regex.Matches(text, @"\d+(?:\.\d+)?", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            .Select(m => decimal.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
    }
    public static IReadOnlyDictionary<string, decimal> Delta(EquippedGear candidate, IEnumerable<EquippedGear> replaced)
    {
        var old = new Dictionary<string, decimal>();
        var previous = replaced.ToArray();
        var unknown = (candidate.UnknownStats ?? []).Concat(previous.SelectMany(g => g.UnknownStats ?? [])).ToHashSet();
        foreach (var gear in previous)
        foreach (var (key, value) in gear.Stats) old[key] = old.GetValueOrDefault(key) + value;
        return candidate.Stats.Keys.Union(old.Keys).Where(k => !unknown.Contains(k)).ToDictionary(k => k,
            k => candidate.Stats.GetValueOrDefault(k) - old.GetValueOrDefault(k));
    }
}
