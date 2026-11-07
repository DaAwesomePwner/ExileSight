using System.Text.Json.Serialization;
using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.Core.Settings;

public sealed class ProfileCatalog : VersionedDocument
{
    public string ActiveProfileId { get; set; } = "default";
    public List<ProfileInfo> Profiles { get; set; } = [new("default", "Default")];
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SettingsDocument))]
[JsonSerializable(typeof(ProfileCatalog))]
public partial class SettingsJsonContext : JsonSerializerContext;
