using System.Text.Json.Serialization;
using ExileSight.Core.Items;
using ExileSight.Core.DataPacks;

namespace ExileSight.Features.ItemInfo;

public enum InfoColor { Unknown, Low, Medium, High, Best, Crafted, Unique }
public enum Highlight { None, Desired, Undesired }
public sealed record ModInfo(ModLine Source, string TierLabel, InfoColor TierColor,
    decimal? RollPosition, InfoColor RollColor, int? RequiredLevel, Highlight Highlight);
public sealed record DefenceInfo(string Name, decimal? Percentile);
public sealed record ItemInfoResult(ParsedItem Item, IReadOnlyList<ModInfo> Mods,
    int? Prefixes, int? Suffixes, IReadOnlyList<DefenceInfo> Defences, int? UniqueDropTier = null)
{
    /// <summary>Card-building context only; excluded from serialized analysis and result equality.</summary>
    [JsonIgnore]
    public ItemInfoSettings Settings { get; init; } = ItemInfoSettings.Default;

    [JsonIgnore]
    public IReadOnlyDictionary<ModLine, StatMatch[]>? Matches { get; init; }

    // Preserve the original record semantics for the six analysis fields, including list identity.
    public bool Equals(ItemInfoResult? other) => other is not null &&
        EqualityComparer<ParsedItem>.Default.Equals(Item, other.Item) &&
        EqualityComparer<IReadOnlyList<ModInfo>>.Default.Equals(Mods, other.Mods) &&
        Prefixes == other.Prefixes && Suffixes == other.Suffixes &&
        EqualityComparer<IReadOnlyList<DefenceInfo>>.Default.Equals(Defences, other.Defences) &&
        UniqueDropTier == other.UniqueDropTier;

    public override int GetHashCode() => HashCode.Combine(Item, Mods, Prefixes, Suffixes, Defences, UniqueDropTier);
}
