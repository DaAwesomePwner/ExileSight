// Frozen from 4ca85cc ItemPackIdentity.cs; namespace changed only.
using ExileSight.Core.DataPacks;
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo.Tests.Reference;

internal static class ItemPackIdentity
{
    // RePoE pinned export uses singular/internal classes; clipboard uses English display plurals.
    public static string ClassKey(string value) => value switch
    {
        "Staves" => "staff", "Warstaves" => "warstaff", "Cluster Jewels" => "jewel",
        _ => value.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant().TrimEnd('s')
    };

    // Slot tags are class-specific, not inherited from generic base tags (e.g. two-hand swords carry "sword").
    private static string? InfluenceSlot(string itemClass) => itemClass switch
    {
        "twohandsword" => "2h_sword", "twohandaxe" => "2h_axe", "twohandmace" => "2h_mace",
        "runedagger" => "rune_dagger", "warstaff" => "warstaff", "staff" => "staff",
        "thrustingonehandsword" or "onehandsword" => "sword", "onehandaxe" => "axe", "onehandmace" => "mace",
        "bow" => "bow", "wand" => "wand", "sceptre" => "sceptre", "claw" => "claw", "dagger" => "dagger",
        "shield" => "shield", "quiver" => "quiver", "helmet" => "helmet", "glove" => "gloves",
        "boot" => "boots", "bodyarmour" => "body_armour", "belt" => "belt", "amulet" => "amulet", "ring" => "ring",
        _ => null
    };

    // Spawn eligibility uses the first matching entry, including zero-weight exclusions.
    // Influence suffixes follow RePoE d0bc438357d849bb93ad22f3db0b12938aaf6da1.
    public static HashSet<string> SpawnTags(ParsedItem item, BaseEntry basis)
    {
        var tags = basis.Tags.ToHashSet(StringComparer.Ordinal);
        var slot = InfluenceSlot(ClassKey(item.ItemClass));
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
            if (slot is not null) tags.Add(slot + "_" + suffix);
        }
        return tags;
    }
}
