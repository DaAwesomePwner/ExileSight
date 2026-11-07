using ExileSight.Core.DataPacks;
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo;

internal static class ItemPackIdentity
{
    // RePoE pinned export uses singular/internal classes; clipboard uses English display plurals.
    public static string ClassKey(string value) => value switch
    {
        "Staves" => "staff", "Warstaves" => "warstaff", "Cluster Jewels" => "jewel",
        _ => value.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant().TrimEnd('s')
    };

    // Spawn eligibility uses the first matching entry, including zero-weight exclusions.
    // Influence suffixes follow RePoE d0bc438357d849bb93ad22f3db0b12938aaf6da1.
    public static HashSet<string> SpawnTags(ParsedItem item, BaseEntry basis)
    {
        var tags = basis.Tags.ToHashSet(StringComparer.Ordinal);
        string[] slots = ["helmet", "body_armour", "gloves", "boots", "shield", "amulet", "belt", "ring",
            "quiver", "sword", "2h_sword", "axe", "2h_axe", "mace", "2h_mace", "sceptre", "dagger",
            "rune_dagger", "claw", "bow", "wand", "staff", "warstaff"];
        foreach (var influence in item.Influences)
        {
            var name = influence.ToLowerInvariant();
            var suffix = name switch
            {
                "redeemer" => "eyrie", "hunter" => "basilisk", "warlord" => "adjudicator",
                "shaper" or "elder" or "crusader" => name, _ => null
            };
            if (suffix is null) continue;
            tags.Add(name);
            tags.Add(suffix);
            foreach (var slot in slots.Where(basis.Tags.Contains)) tags.Add(slot + "_" + suffix);
        }
        return tags;
    }
}
