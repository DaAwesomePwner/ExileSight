using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo;

// Reverse local defence formula: Exile-UI modules/item-checker.ahk:412–465 (MIT).
internal static class DefenceCalculator
{
    private static readonly (string Property, string Key, string Flat, string Increased)[] Kinds =
    [
        ("Armour", "armour", "local_base_physical_damage_reduction_rating", "local_physical_damage_reduction_rating_+%"),
        ("Evasion Rating", "evasion", "local_base_evasion_rating", "local_evasion_rating_+%"),
        ("Energy Shield", "energy_shield", "local_energy_shield", "local_energy_shield_+%"),
        ("Ward", "ward", "local_base_ward", "local_ward_+%")
    ];
    private static readonly HashSet<string> Supported = Kinds.SelectMany(k => new[] { k.Flat, k.Increased })
        .Concat(["local_armour_and_evasion_+%", "local_armour_and_energy_shield_+%",
            "local_evasion_and_energy_shield_+%"]).ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<DefenceInfo> Calculate(ParsedItem item, IItemInfoData? data)
    {
        var basis = data?.FindBase(item);
        var values = item.Mods.Select(m => data?.MatchLine(m) ?? []).ToArray();
        // Any unrecognised modifier could alter local defence. Never silently treat it as zero.
        var complete = !item.Unidentified && values.All(v => v.Length == 1 && v[0].Values.All(s =>
            s.Min == s.Max && (Supported.Contains(s.StatId) ||
                !new[] { "armour", "evasion", "energy_shield", "ward", "defence" }
                    .Any(word => s.StatId.Contains(word, StringComparison.Ordinal)))));
        return Kinds.Where(k => item.Properties.ContainsKey(k.Property)).Select(k =>
        {
            decimal? percentile = null;
            if (complete && basis?.Defences.TryGetValue(k.Key, out var range) == true &&
                item.Properties[k.Property].Values.FirstOrDefault() is { } displayed)
            {
                var stats = values.SelectMany(v => v[0].Values).ToArray();
                var flat = stats.Where(v => v.StatId == k.Flat).Sum(v => v.Min);
                var increased = stats.Where(v => v.StatId == k.Increased ||
                    v.StatId == "local_armour_and_evasion_+%" && k.Key is "armour" or "evasion" ||
                    v.StatId == "local_armour_and_energy_shield_+%" && k.Key is "armour" or "energy_shield" ||
                    v.StatId == "local_evasion_and_energy_shield_+%" && k.Key is "evasion" or "energy_shield").Sum(v => v.Min);
                try
                {
                    var multiplier = (1 + (item.Quality ?? 0) / 100) * (1 + increased / 100);
                    if (multiplier > 0)
                    {
                        var raw = displayed.Value / multiplier - flat;
                        percentile = range.Max == range.Min ? 100 : (raw - range.Min) * 100 / (range.Max - range.Min);
                        if (percentile is < 0 or > 100) percentile = null;
                    }
                }
                catch (OverflowException) { /* Untrusted numeric copy: retain unknown. */ }
            }
            return new DefenceInfo(k.Property, percentile);
        }).ToArray();
    }
}
