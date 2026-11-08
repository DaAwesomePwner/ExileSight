using System.Text.Json;
using ExileSight.Core.Features;

namespace ExileSight.Features.MapInfo;

public sealed record MapInfoSettings(decimal QuantityThreshold = 70, decimal RarityThreshold = 40, decimal PackSizeThreshold = 25)
{
    public MapInfoSettings Validate()
    {
        if (QuantityThreshold < 0 || RarityThreshold < 0 || PackSizeThreshold < 0)
            throw new ArgumentException("Map thresholds must be nonnegative percentages.");
        return this;
    }
    public static MapInfoSettings Read(JsonElement? value)
    {
        try { return value?.Deserialize<MapInfoSettings>()?.Validate() ?? new(); }
        catch (Exception e) when (e is JsonException or ArgumentException) { return new(); }
    }
    public static MapInfoSettings Load(IFeatureContext context) => Read(context.ReadSetting("settings"));
    public void Save(IFeatureContext context) => context.WriteSetting("settings", JsonSerializer.SerializeToElement(Validate()));
}
