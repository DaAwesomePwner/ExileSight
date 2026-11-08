using ExileSight.Core.Features;
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo;

public enum CardSlotKind { Unknown, Prefix, Suffix, Implicit, None }
public enum CardLevelKind { Unknown, Known, NotApplicable }
public sealed record ItemCardName(string? BaseType, string? UniqueName, ItemRarity Rarity,
    string ItemClass, int? ItemLevel, string FullName)
{
    public string DisplayName => UniqueName ?? BaseType ?? FullName;
}
public sealed record ItemCardVerdict(int Wanted, int Unwanted, decimal? AverageRoll);
public sealed record ItemCardPips(int? Count, int? Maximum)
{
    public int? Open => Count is int count && Maximum is int max && count <= max ? max - count : null;
}
public sealed record ItemCardSlots(ItemCardPips Prefixes, ItemCardPips Suffixes);
/// <summary>GameLineStart is zero-based in ParsedItem.Mods, even when implicits are hidden.</summary>
public sealed record ItemCardRow(CardSlotKind Slot, string Label, string Tier, InfoColor TierTone,
    decimal? RollPosition, InfoColor RollTone, CardLevelKind LevelKind, int? RequiredLevel,
    Highlight Highlight, bool Crafted, bool Fixed, int GameLineStart, int GameLineCount);
/// <summary>Independent sections allow the view to apply its height cap without recalculating data.</summary>
public sealed record ItemCard(ItemCardName Name, bool Unidentified, bool NeedsAdvancedCopy,
    ItemCardVerdict? Verdict, ItemCardSlots? Slots, IReadOnlyList<DefenceInfo> Defences,
    IReadOnlyList<ItemCardRow> Rows, bool ShowItemLevelColumn, ItemCardComparison? Compare);
