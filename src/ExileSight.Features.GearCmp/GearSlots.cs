// Provenance: Exile-UI modules/item-checker.ahk:865–1100; explicit slots replace pixel placement (D-006).
using ExileSight.Core.Games;
using ExileSight.Core.Items;

namespace ExileSight.Features.GearCmp;

public enum GearSlot { Helmet, Body, Gloves, Boots, Belt, Amulet, LeftRing, RightRing, MainHand, OffHand }
public static class GearSlots
{
    public static bool TwoHanded(string itemClass) => itemClass is "Two Hand Swords" or "Two Hand Axes"
        or "Two Hand Maces" or "Staves" or "Warstaves" or "Bows";
    public static GearSlot[] For(ParsedItem item)
    {
        if (item.Game != Game.PoE1 || item.Rarity is not (ItemRarity.Normal or ItemRarity.Magic or ItemRarity.Rare or ItemRarity.Unique))
            return [];
        return item.ItemClass switch
        {
            "Helmets" => [GearSlot.Helmet], "Body Armours" => [GearSlot.Body],
            "Gloves" => [GearSlot.Gloves], "Boots" => [GearSlot.Boots], "Belts" => [GearSlot.Belt],
            "Amulets" => [GearSlot.Amulet], "Rings" => [GearSlot.LeftRing, GearSlot.RightRing],
            "Shields" or "Quivers" => [GearSlot.OffHand],
            "One Hand Swords" or "Thrusting One Hand Swords" or "One Hand Axes" or "One Hand Maces"
                or "Sceptres" or "Daggers" or "Rune Daggers" or "Claws" or "Wands" => [GearSlot.MainHand, GearSlot.OffHand],
            _ when TwoHanded(item.ItemClass) => [GearSlot.MainHand],
            _ => []
        };
    }
    public static string Label(GearSlot slot) => slot switch
    {
        GearSlot.LeftRing => "left ring", GearSlot.RightRing => "right ring",
        GearSlot.MainHand => "main hand", GearSlot.OffHand => "off hand", _ => slot.ToString().ToLowerInvariant()
    };
}
