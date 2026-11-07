using ExileSight.Core.Games;

namespace ExileSight.Core.Items;

public enum ItemRarity { Unknown, Normal, Magic, Rare, Unique, Currency, Gem, DivinationCard }
public enum WarningSeverity { Warning, Error }
public sealed record ParseWarning(WarningSeverity Severity, string Message);
public sealed record ItemParseResult(ParsedItem? Item, IReadOnlyList<ParseWarning> Warnings);
public sealed record PriceNote(string RawText, string Kind, decimal Amount, string Currency);
public sealed record ItemStack(long Current, long Maximum);
public sealed record ItemProperty(string Text, IReadOnlyList<ModValue> Values);

/// <summary>Text-derived information only. Base defences and affix requirements require data packs.</summary>
public sealed class ParsedItem
{
    public Game Game { get; internal set; }
    public string ItemClass { get; internal set; } = "";
    public ItemRarity Rarity { get; internal set; }
    public string Name { get; internal set; } = "";
    public string? BaseType { get; internal set; }
    public int? ItemLevel { get; internal set; }
    public decimal? Quality { get; internal set; }
    public int? GemLevel { get; internal set; }
    public ItemStack? GemExperience { get; internal set; }
    public ItemStack? StackSize { get; internal set; }
    public int? MapTier { get; internal set; }
    public decimal? MapQuantity { get; internal set; }
    public decimal? MapRarity { get; internal set; }
    public decimal? MapPackSize { get; internal set; }
    public Dictionary<string, int> Requirements { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, ItemProperty> Properties { get; } = new(StringComparer.Ordinal);
    public List<string> SocketGroups { get; } = [];
    public int SocketCount => SocketGroups.Sum(g => g.Split('-').Length);
    public int MaximumLinks => SocketGroups.Count == 0 ? 0 : SocketGroups.Max(g => g.Split('-').Length);
    public bool Corrupted { get; internal set; }
    public bool Mirrored { get; internal set; }
    public bool Unidentified { get; internal set; }
    public bool Fractured { get; internal set; }
    public bool Synthesised { get; internal set; }
    public bool Split { get; internal set; }
    public bool Unmodifiable { get; internal set; }
    public List<string> Influences { get; } = [];
    public string? Anointment { get; internal set; }
    public string? Note { get; internal set; }
    public PriceNote? Price { get; internal set; }
    public List<ModLine> Mods { get; } = [];
    public IEnumerable<ModLine> ImplicitMods => Mods.Where(m => m.Kind.HasFlag(ModKind.Implicit));
    public IEnumerable<ModLine> ExplicitMods => Mods.Where(m => m.Kind.HasFlag(ModKind.Explicit));
    public IEnumerable<ModLine> CraftedMods => Mods.Where(m => m.Kind.HasFlag(ModKind.Crafted));
    public IEnumerable<ModLine> EnchantMods => Mods.Where(m => m.Kind.HasFlag(ModKind.Enchant));
    public IEnumerable<ModLine> FracturedMods => Mods.Where(m => m.Kind.HasFlag(ModKind.Fractured));
    public IEnumerable<ModLine> ScourgeMods => Mods.Where(m => m.Kind.HasFlag(ModKind.Scourge));
    public List<string> FlavourText { get; } = [];
    public List<string> OtherText { get; } = [];
}
