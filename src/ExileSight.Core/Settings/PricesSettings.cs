namespace ExileSight.Core.Settings;

/// <summary>Additive schema-1 section. Null league selects the current challenge league, falling back to Standard.</summary>
public sealed class PricesSettings : ExtensibleSettings
{
    public string? League { get; set; }
    public bool RefreshEnabled { get; set; } = true;
    public int RefreshMinutes { get; set; } = 60;
    [System.Text.Json.Serialization.JsonIgnore]
    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(Math.Max(60, RefreshMinutes));
}
