using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExileSight.Core.DataPacks;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(DataPack))]
[JsonSerializable(typeof(PackManifest))]
[JsonSerializable(typeof(PackContent))]
public partial class PackJson : JsonSerializerContext;

public static class PackCodec
{
    public const int SchemaVersion = 1;
    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static byte[] ContentBytes(PackContent content) => JsonSerializer.SerializeToUtf8Bytes(content, PackJson.Default.PackContent);
    public static bool IsValid(DataPack pack, string packId)
    {
        var m = pack.Manifest;
        var c = pack.Content;
        return m is not null && c is not null && m.PackId == packId && c.PackId == packId &&
            m.SchemaVersion == SchemaVersion && c.SchemaVersion == SchemaVersion &&
            m.Game is Games.Game.PoE1 or Games.Game.PoE2 && c.Bases is not null && c.Mods is not null &&
            m.Source is not null && !string.IsNullOrWhiteSpace(m.PackVersion) && m.Game == c.Game && packId.StartsWith(m.Game == Games.Game.PoE1 ? "poe1/" : "poe2/", StringComparison.Ordinal) &&
            m.PackVersion == c.PackVersion && m.ContentSha256 == Hash(ContentBytes(c));
    }
}
