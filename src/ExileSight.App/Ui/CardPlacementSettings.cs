using ExileSight.Core.Overlay;
using ExileSight.Core.Settings;

namespace ExileSight.App.Ui;

public static class CardPlacementSettings
{
    public static CardPlacementOptions ToCardPlacementOptions(this OverlaySettings settings) => new()
    { PlacementMode = settings.PlacementMode, DockedX = settings.DockedX, DockedY = settings.DockedY };
}
