// Frozen from 4ca85cc ItemInfoCalculator.cs; namespace/reference class name changed only.
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo.Tests.Reference;

// Algorithms ported from Exile-UI modules/item-checker.ahk:1437,1495–1499,1641–1717,2788–2831 (MIT).
internal sealed class ReferenceItemInfoCalculator(IItemInfoData? data = null)
{
    public ItemInfoResult Calculate(ParsedItem item, ItemInfoSettings? settings = null)
    {
        settings = (settings ?? ItemInfoSettings.Default).Validate();
        var rows = item.Mods.Select(line =>
        {
            var group = line.Group is null ? [line] : item.Mods.Where(m => m.Group == line.Group).ToArray();
            var matched = data?.FindMod(item, group);
            var roll = Roll(line, settings.InvertedLines?.Any(rule => ModRuleKey.Normalize(rule) == ModRuleKey.Normalize(line.Text)) == true);
            var crafted = line.Kind.HasFlag(ModKind.Crafted);
            var implicitMod = line.Kind.HasFlag(ModKind.Implicit);
            var enchant = line.Kind.HasFlag(ModKind.Enchant);
            var unique = !implicitMod && !enchant && (line.Kind.HasFlag(ModKind.Unique) || item.Rarity == ItemRarity.Unique);
            var tier = implicitMod ? (line.Tier is int t ? $"Implicit T{t}" : "Implicit") : enchant ? "Enchant" : crafted ? "Crafted" : unique ? "Unique" : line.Tier is int n ? $"T{n}" : "Unknown";
            var color = crafted ? InfoColor.Crafted : unique ? InfoColor.Unique :
                line.Tier == 1 ? InfoColor.Best : line.Tier <= 3 ? InfoColor.High :
                line.Tier is not null ? InfoColor.Medium : InfoColor.Unknown;
            return new ModInfo(line, tier, color, roll, RollColor(roll), matched?.RequiredLevel,
                settings.Evaluate(item, line));
        }).ToArray();
        var explicitLines = item.ExplicitMods.ToArray();
        var known = !item.Unidentified && (item.Rarity == ItemRarity.Normal && explicitLines.Length == 0 || explicitLines.Length > 0 &&
            explicitLines.All(m => m.Group is not null && m.AffixType != AffixType.Unknown));
        int? Count(AffixType type) => known ? explicitLines.Where(m => m.AffixType == type).Select(m => m.Group).Distinct().Count() : null;
        return new(item, rows, Count(AffixType.Prefix), Count(AffixType.Suffix),
            DefenceCalculator.Calculate(item, data));
    }

    public static decimal? Roll(ModLine line, bool invert)
    {
        var values = line.Values.Where(v => v.Minimum is not null && v.Maximum is not null).ToArray();
        if (values.Length == 0) return null;
        try
        {
            var current = values.Sum(v => v.Value);
            var min = values.Sum(v => v.Minimum!.Value);
            var max = values.Sum(v => v.Maximum!.Value);
            if (invert) (min, max) = (max, min);
            return min == max ? 100 : 100 * Math.Abs(current - min) / Math.Abs(max - min);
        }
        catch (OverflowException) { return null; }
    }
    private static InfoColor RollColor(decimal? roll) => roll switch
    {
        null => InfoColor.Unknown, >= 100 => InfoColor.Best, >= 75 => InfoColor.High,
        >= 25 => InfoColor.Medium, _ => InfoColor.Low
    };
}
