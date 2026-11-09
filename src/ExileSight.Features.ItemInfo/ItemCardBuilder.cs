using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo;

/// <summary>Build once per copied item using the calculator's per-line matches.</summary>
public static class ItemCardBuilder
{
    public static ItemCard Build(ItemInfoResult result, IItemInfoData? data = null,
        ItemInfoSettings? settings = null, ItemCardComparison? comparison = null)
    {
        settings = (settings ?? result.Settings).Validate();
        var item = result.Item;
        var unique = item.Rarity == ItemRarity.Unique;
        // With no mods, copy mode is unknowable and there is nothing to request ranges for.
        var advanced = false;
        foreach (var mod in result.Mods)
            if (!string.IsNullOrWhiteSpace(mod.Source.Header)) { advanced = true; break; }
        var rows = new List<ItemCardRow>(result.Mods.Count);
        var wanted = 0; var unwanted = 0; var rollCount = 0; decimal rollSum = 0;
        var seen = new HashSet<int>();
        if (!item.Unidentified)
        for (var i = 0; i < result.Mods.Count; i++)
        {
            var first = result.Mods[i];
            var group = first.Source.Group;
            if (group is int id && !seen.Add(id)) continue;
            var lines = new List<ModInfo>();
            if (group is null) lines.Add(first);
            else foreach (var mod in result.Mods) if (mod.Source.Group == group) lines.Add(mod);
            var row = Row(lines, i, unique, advanced, data, result.Matches);
            if (row.Highlight == Highlight.Desired) wanted++;
            if (row.Highlight == Highlight.Undesired) unwanted++;
            if (row.RollPosition is { } position) { rollSum += position; rollCount++; }
            if (settings.ShowImplicits || row.Slot != CardSlotKind.Implicit) rows.Add(row);
        }
        // Count mods, not individual game lines or only the rows visible after toggles.
        ItemCardVerdict? verdict = item.Unidentified ? null : unique
            ? rollCount == 0 ? null : new(0, 0, rollSum / rollCount)
            : settings.Profiles.Length == 0 || wanted + unwanted == 0 ? null : new(wanted, unwanted, null);
        var max = Maximum(item);
        return new(new(item.BaseType, unique ? item.Name : null, item.Rarity, item.ItemClass, item.ItemLevel, item.Name),
            item.Unidentified, !item.Unidentified && result.Mods.Count > 0 && !advanced, verdict,
            unique || item.Rarity == ItemRarity.Normal || item.Unidentified ? null : new(new(advanced ? result.Prefixes : null, max),
                new(advanced ? result.Suffixes : null, max)),
            settings.ShowDefenceBasePercentile ? result.Defences : [],
            rows,
            settings.ShowItemLevelColumn, settings.ShowCompare && !item.Unidentified ? comparison : null);
    }

    private static ItemCardRow Row(IReadOnlyList<ModInfo> lines, int start, bool unique, bool advanced, IItemInfoData? data,
        IReadOnlyDictionary<ModLine, StatMatch[]>? perLine)
    {
        var first = lines[0];
        var source = first.Source;
        var implicitMod = source.Kind.HasFlag(ModKind.Implicit);
        var crafted = source.Kind.HasFlag(ModKind.Crafted);
        var enchant = source.Kind.HasFlag(ModKind.Enchant);
        var uniqueMod = unique || source.Kind.HasFlag(ModKind.Unique);
        var slot = enchant ? CardSlotKind.None : implicitMod ? CardSlotKind.Implicit : uniqueMod ? CardSlotKind.None : source.AffixType switch
        {
            AffixType.Prefix => CardSlotKind.Prefix, AffixType.Suffix => CardSlotKind.Suffix, _ => CardSlotKind.Unknown
        };
        // Join every game line's ordered IDs. Ambiguous signatures deliberately use the text fallback.
        var ids = new List<string>();
        foreach (var line in lines)
        {
            var matches = perLine is not null && perLine.TryGetValue(line.Source, out var cached)
                ? cached : data?.MatchLine(line.Source) ?? [];
            if (matches.Length == 0) { ids.Clear(); break; }
            var signature = matches[0].Values;
            var ambiguous = false;
            for (var m = 1; m < matches.Length && !ambiguous; m++)
            {
                var candidate = matches[m].Values;
                if (candidate.Length != signature.Length) { ambiguous = true; break; }
                for (var v = 0; v < signature.Length; v++)
                    if (candidate[v].StatId != signature[v].StatId) { ambiguous = true; break; }
            }
            if (ambiguous) { ids.Clear(); break; }
            foreach (var value in signature) ids.Add(value.StatId);
        }
        decimal sum = 0; var count = 0;
        var fixedLine = advanced && uniqueMod;
        var highlight = Highlight.None;
        foreach (var line in lines)
        {
            if (line.RollPosition is { } position) { sum += position; count++; }
            foreach (var value in line.Source.Values)
                if (value.Minimum is not null && value.Maximum is not null) fixedLine = false;
            if (line.Highlight == Highlight.Undesired) highlight = Highlight.Undesired;
            else if (line.Highlight == Highlight.Desired && highlight == Highlight.None) highlight = Highlight.Desired;
        }
        decimal? roll = advanced && count > 0 ? sum / count : null;
        var levelKind = implicitMod || crafted || uniqueMod ? CardLevelKind.NotApplicable :
            advanced && first.RequiredLevel is not null ? CardLevelKind.Known : CardLevelKind.Unknown;
        var tier = implicitMod ? "imp" : enchant ? "ench" : fixedLine ? "·" : crafted ? "cr" : uniqueMod ? "U" :
            advanced && source.Tier is int t ? $"T{t}" : "T?";
        return new(slot, ModLabels.For(ids, StatText.StripCopySuffixes(source.Text)), tier,
            fixedLine ? InfoColor.Unknown : first.TierColor, roll, ItemInfoCalculator.RollColor(roll),
            levelKind, levelKind == CardLevelKind.Known ? first.RequiredLevel : null,
            highlight, crafted, fixedLine, start, lines.Count);
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
