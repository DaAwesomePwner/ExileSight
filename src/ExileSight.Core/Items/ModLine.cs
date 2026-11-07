namespace ExileSight.Core.Items;

[Flags]
public enum ModKind { Explicit = 1, Implicit = 2, Crafted = 4, Fractured = 8, Enchant = 16, Scourge = 32, Veiled = 64, Unique = 128, Eater = 256, Exarch = 512 }
public enum AffixType { Unknown, Prefix, Suffix }
public sealed record ModValue(decimal Value, decimal? Minimum = null, decimal? Maximum = null);
/// <summary>Group identifies lines sharing an advanced header (including hybrid affixes).</summary>
public sealed record ModLine(string Text, ModKind Kind, AffixType AffixType,
    int? Tier, string? AffixName, IReadOnlyList<ModValue> Values, int? Group = null, string? Header = null);
