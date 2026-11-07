namespace ExileSight.Core.Items;

// English labels informed by Exile-UI data/english/client.txt (items_*), MIT.
// Keep language-dependent labels here; no upstream data-pack contents are included.
internal static class EnglishItemPatterns
{
    internal const string Separator = "--------";
    internal const string ItemClass = "Item Class: ";
    internal const string Rarity = "Rarity: ";
    internal static readonly string[] Influences = ["Shaper", "Elder", "Crusader", "Hunter", "Redeemer", "Warlord", "Searing Exarch", "Eater of Worlds"];
    internal static readonly string[] PropertyNames = ["Armour", "Evasion Rating", "Energy Shield", "Ward",
        "Physical Damage", "Elemental Damage", "Chaos Damage", "Critical Strike Chance", "Attacks per Second",
        "Weapon Range", "Quality", "Level", "Experience", "Stack Size", "Item Level", "Map Tier",
        "Item Quantity", "Item Rarity", "Monster Pack Size", "Mana Cost", "Cast Time", "Cooldown Time",
        "Requires", "Charges", "Currently has"];
}
