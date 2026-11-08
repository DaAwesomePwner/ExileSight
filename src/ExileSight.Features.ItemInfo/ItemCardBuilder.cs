using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo;

/// <summary>Build once per copied item; leaves the legacy tooltip result untouched.</summary>
public static class ItemCardBuilder
{
    public static ItemCard Build(ItemInfoResult result, IItemInfoData? data = null,
        ItemInfoSettings? settings = null, ItemCardComparison? comparison = null)
    {
        settings = (settings ?? ItemInfoSettings.Default).Validate();
        var item = result.Item;
        var unique = item.Rarity == ItemRarity.Unique;
        var advanced = result.Mods.Any(m => m.Source.Header is not null);
        var rows = new List<ItemCardRow>();
        var seen = new HashSet<int>();
        if (!item.Unidentified)
        for (var i = 0; i < result.Mods.Count; i++)
        {
            var first = result.Mods[i];
            var group = first.Source.Group;
            if (group is int id && !seen.Add(id)) continue;
            var lines = group is null ? [first] : result.Mods.Where(m => m.Source.Group == group).ToArray();
            rows.Add(Row(lines, i, unique, advanced, data));
        }
        // Count mods, not individual game lines or only the rows visible after toggles.
        var wanted = rows.Count(r => r.Highlight == Highlight.Desired);
        var unwanted = rows.Count(r => r.Highlight == Highlight.Undesired);
        var rolls = rows.Where(r => r.RollPosition is not null).Select(r => r.RollPosition!.Value).ToArray();
        ItemCardVerdict? verdict = item.Unidentified ? null : unique
            ? rolls.Length == 0 ? null : new(0, 0, rolls.Average())
            : settings.Profiles.Length == 0 || wanted + unwanted == 0 ? null : new(wanted, unwanted, null);
        var max = Maximum(item);
        return new(new(item.BaseType, unique ? item.Name : null, item.Rarity, item.ItemClass, item.ItemLevel),
            item.Unidentified, !item.Unidentified && !advanced, verdict,
            unique || item.Unidentified ? null : new(new(advanced ? result.Prefixes : null, max),
                new(advanced ? result.Suffixes : null, max)),
            settings.ShowDefenceBasePercentile ? result.Defences : [],
            settings.ShowImplicits ? rows : rows.Where(r => r.Slot != CardSlotKind.Implicit).ToArray(),
            settings.ShowItemLevelColumn, settings.ShowCompare && !item.Unidentified ? comparison : null);
    }

    private static ItemCardRow Row(ModInfo[] lines, int start, bool unique, bool advanced, IItemInfoData? data)
    {
        var first = lines[0];
        var source = first.Source;
        var implicitMod = source.Kind.HasFlag(ModKind.Implicit);
        var crafted = source.Kind.HasFlag(ModKind.Crafted);
        var enchant = source.Kind.HasFlag(ModKind.Enchant);
        var uniqueMod = unique || source.Kind.HasFlag(ModKind.Unique);
        var slot = implicitMod ? CardSlotKind.Implicit : source.AffixType switch
        {
            AffixType.Prefix => CardSlotKind.Prefix, AffixType.Suffix => CardSlotKind.Suffix, _ => CardSlotKind.Unknown
        };
        // Join every game line's ordered IDs. Ambiguous signatures deliberately use the text fallback.
        var ids = new List<string>();
        foreach (var line in lines)
        {
            var matches = data?.MatchLine(line.Source) ?? [];
            var signatures = matches.Select(m => m.Values.Select(v => v.StatId).ToArray())
                .DistinctBy(s => string.Join("\n", s)).ToArray();
            if (signatures.Length != 1) { ids.Clear(); break; }
            ids.AddRange(signatures[0]);
        }
        var knownRolls = lines.Where(m => m.RollPosition is not null).Select(m => m.RollPosition!.Value).ToArray();
        decimal? roll = advanced && knownRolls.Length > 0 ? knownRolls.Average() : null;
        var fixedLine = advanced && uniqueMod && lines.All(m => m.Source.Values.All(v => v.Minimum is null || v.Maximum is null));
        var levelKind = implicitMod || crafted || uniqueMod ? CardLevelKind.NotApplicable :
            advanced && first.RequiredLevel is not null ? CardLevelKind.Known : CardLevelKind.Unknown;
        var tier = fixedLine ? "·" : implicitMod ? "imp" : enchant ? "ench" : crafted ? "cr" : uniqueMod ? "U" :
            advanced && source.Tier is int t ? $"T{t}" : "T?";
        var highlight = lines.Any(m => m.Highlight == Highlight.Undesired) ? Highlight.Undesired :
            lines.Any(m => m.Highlight == Highlight.Desired) ? Highlight.Desired : Highlight.None;
        return new(slot, ModLabels.For(ids, StatText.StripCopySuffixes(source.Text)), tier,
            fixedLine ? InfoColor.Unknown : first.TierColor, roll, ItemInfoCalculator.RollColor(roll),
            levelKind, levelKind == CardLevelKind.Known ? first.RequiredLevel : null,
            highlight, crafted, fixedLine, start, lines.Length);
    }

    private static int? Maximum(ParsedItem item)
    {
        var knownClass = item.ItemClass is "Body Armours" or "Helmets" or "Gloves" or "Boots" or "Shields"
            or "Rings" or "Amulets" or "Belts" or "Quivers" or "Bows" or "Staves" or "Warstaves"
            or "One Hand Swords" or "Thrusting One Hand Swords" or "One Hand Axes" or "One Hand Maces"
            or "Two Hand Swords" or "Two Hand Axes" or "Two Hand Maces" or "Sceptres" or "Daggers"
            or "Rune Daggers" or "Claws" or "Wands" or "Jewels" or "Abyss Jewels" or "Cluster Jewels"
            or "Life Flasks" or "Mana Flasks" or "Hybrid Flasks" or "Utility Flasks";
        if (!knownClass) return null;
        if (item.Rarity == ItemRarity.Magic) return 1;
        if (item.Rarity != ItemRarity.Rare) return null;
        return item.ItemClass is "Jewels" or "Abyss Jewels" or "Cluster Jewels" ? 2 : 3;
    }
}
