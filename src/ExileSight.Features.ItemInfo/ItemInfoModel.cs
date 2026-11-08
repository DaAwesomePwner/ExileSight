using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo;

public enum InfoColor { Unknown, Low, Medium, High, Best, Crafted, Unique }
public enum Highlight { None, Desired, Undesired }
public sealed record ModInfo(ModLine Source, string TierLabel, InfoColor TierColor,
    decimal? RollPosition, InfoColor RollColor, int? RequiredLevel, Highlight Highlight);
public sealed record DefenceInfo(string Name, decimal? Percentile);
public sealed record ItemInfoResult(ParsedItem Item, IReadOnlyList<ModInfo> Mods,
    int? Prefixes, int? Suffixes, IReadOnlyList<DefenceInfo> Defences, int? UniqueDropTier = null)
{
    /// <summary>The validated settings used to calculate highlights and rolls.</summary>
    public ItemInfoSettings Settings { get; init; } = ItemInfoSettings.Default;
}
