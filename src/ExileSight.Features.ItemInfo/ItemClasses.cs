namespace ExileSight.Features.ItemInfo;

public static class ItemClasses
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "Helmets", "Body Armours", "Gloves", "Boots", "Shields", "Quivers", "Belts", "Amulets", "Rings",
        "One Hand Swords", "Thrusting One Hand Swords", "Two Hand Swords", "One Hand Axes", "Two Hand Axes",
        "One Hand Maces", "Two Hand Maces", "Sceptres", "Daggers", "Rune Daggers", "Claws", "Wands",
        "Staves", "Warstaves", "Bows", "Jewels", "Abyss Jewels", "Cluster Jewels",
        "Life Flasks", "Mana Flasks", "Hybrid Flasks", "Utility Flasks"
    };
    public static bool Supports(string name) => Names.Contains(name);
    public static string Canonicalize(string name) => Names.FirstOrDefault(c =>
        string.Equals(c, name.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown item class: {name.Trim()}");
}
