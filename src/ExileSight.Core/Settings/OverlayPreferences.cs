using ExileSight.Core.Overlay;
namespace ExileSight.Core.Settings;

/// <summary>Applies UI choices while retaining unrelated and forward-compatible settings.</summary>
public sealed record OverlayPreferences(CardPlacementMode Placement, double X, double Y, double? Scale,
    OverlayDensity Density, OverlayContrast Contrast)
{
    public void Apply(OverlaySettings settings)
    {
        if (!Enum.IsDefined(Placement) || !Enum.IsDefined(Density) || !Enum.IsDefined(Contrast) ||
            !double.IsFinite(X) || !double.IsFinite(Y)) throw new ArgumentException("Invalid overlay preferences.");
        settings.SetUiScaleOverride(Scale);
        settings.PlacementMode = Placement;
        settings.DockedX = Math.Clamp(X, 0, 1);
        settings.DockedY = Math.Clamp(Y, 0, 1);
        settings.Density = Density;
        settings.Contrast = Contrast;
    }
}
