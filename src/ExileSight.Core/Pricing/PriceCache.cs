using System.Text.Json.Serialization;

namespace ExileSight.Core.Pricing;

internal sealed record PriceCache
{
    [JsonIgnore]
    public bool NeedsRepair { get; set; }
    public DateTimeOffset? LastAttempt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? Json { get; set; }
    public string? ETag { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public string? Error { get; set; }
}

[JsonSerializable(typeof(PriceCache))]
internal partial class PriceJsonContext : JsonSerializerContext;
