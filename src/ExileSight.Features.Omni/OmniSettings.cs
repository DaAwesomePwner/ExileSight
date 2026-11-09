using System.Text.Json;
using ExileSight.Core.Features;

namespace ExileSight.Features.Omni;

public sealed record OmniSettings(bool UseAdvancedCopy = true)
{
    public static OmniSettings Default => new();

    public static OmniSettings Load(IFeatureContext context)
    {
        try { return context.ReadSetting("settings")?.Deserialize<OmniSettings>() ?? Default; }
        catch (JsonException) { return Default; }
    }

    public void Save(IFeatureContext context) =>
        context.WriteSetting("settings", JsonSerializer.SerializeToElement(this));
}
