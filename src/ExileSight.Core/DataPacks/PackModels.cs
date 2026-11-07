using ExileSight.Core.Games;

namespace ExileSight.Core.DataPacks;

public sealed record PackSource(string Repo, string Sha, Dictionary<string, string> Files);
public sealed record PackManifest(string PackId, Game Game, int SchemaVersion, string PackVersion,
    string GamePatch, DateTimeOffset GeneratedAt, PackSource Source, string Attribution, string ContentSha256);
public sealed record NumericRange(int Min, int Max);
public sealed record BaseEntry(string Id, string Name, string ItemClass, string[] Tags, int DropLevel,
    Dictionary<string, int> Requirements, string[] ImplicitIds, Dictionary<string, NumericRange> Defences,
    int? UniqueDropTier = null, string Domain = "item");
public sealed record ModStat(string Id, int Min, int Max);
public sealed record SpawnWeight(string Tag, int Weight);
public sealed record ModTier(string ItemClass, string Tag, int Tier);
public sealed record ModEntry(string Id, string Name, string AffixType, string Domain, string GenerationType,
    int RequiredLevel, string[] Groups, ModStat[] Stats, SpawnWeight[] SpawnWeights,
    string[] TextTemplateIds, ModTier[] Tiers, string Family = "");
public sealed record PackContent(string PackId, Game Game, int SchemaVersion, string PackVersion,
    BaseEntry[] Bases, ModEntry[] Mods);
public sealed record DataPack(PackManifest Manifest, PackContent Content);
