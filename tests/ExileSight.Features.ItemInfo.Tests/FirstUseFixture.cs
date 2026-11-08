using ExileSight.Core.Games;
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo.Tests;

// Synthetic advanced-copy item with a real base/affix, not a captured player's item.
internal static class FirstUseFixture
{
    internal static ParsedItem Parse() => ItemTextParser.Parse("""
        Item Class: Body Armours
        Rarity: Rare
        Test Guard
        Glorious Plate
        --------
        Quality: +20%
        Armour: 960
        --------
        Item Level: 84
        --------
        { Prefix Modifier "Fecund" (Tier: 3) }
        +135(130-144) to maximum Life
        """, Game.PoE1).Item!;
}
