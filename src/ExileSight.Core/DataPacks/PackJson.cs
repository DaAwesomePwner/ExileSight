using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExileSight.Core.DataPacks;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, NewLine = "\n")]
[JsonSerializable(typeof(ModRank[]))]
[JsonSerializable(typeof(DataPack))]
[JsonSerializable(typeof(CachedPack))]
[JsonSerializable(typeof(PackManifest))]
[JsonSerializable(typeof(PackContent))]
public partial class PackJson : JsonSerializerContext;

// Base64 retains the exact content bytes across cache serialization and operating systems.
public sealed record CachedPack(PackManifest Manifest, byte[] ContentBytes, string? ReleaseTag = null,
    DateTimeOffset CheckedAt = default, string? ETag = null);

public static class PackCodec
{
    public const int SchemaVersion = 2;
    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static byte[] ContentBytes(PackContent content) => JsonSerializer.SerializeToUtf8Bytes(content, PackJson.Default.PackContent);
    public static DataPack? Validate(PackManifest m, byte[] bytes, string packId)
    {
        if (m is null || bytes is null || m.ContentSha256 != Hash(bytes)) return null;
        var c = JsonSerializer.Deserialize(bytes, PackJson.Default.PackContent);
        return c is not null && m.PackId == packId && c.PackId == packId &&
            m.SchemaVersion == SchemaVersion && c.SchemaVersion == SchemaVersion &&
            m.Game is Games.Game.PoE1 or Games.Game.PoE2 && c.Bases is not null && c.Mods is not null &&
            m.Source is not null && !string.IsNullOrWhiteSpace(m.PackVersion) && m.Game == c.Game &&
            packId.StartsWith(m.Game == Games.Game.PoE1 ? "poe1/" : "poe2/", StringComparison.Ordinal) &&
            m.PackVersion == c.PackVersion ? new(m, c) : null;
    }
}
