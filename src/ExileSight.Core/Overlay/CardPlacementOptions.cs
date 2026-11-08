namespace ExileSight.Core.Overlay;

public enum CardPlacementMode { BesideCursor = 0, Docked = 1 }

/// <summary>Geometry-only options, independent of persisted overlay settings.</summary>
public sealed record CardPlacementOptions
{
    // Upper-right of centre, leaving a 300-unit card left of the minimap's rightmost 20% at 1080p.
    public static readonly (double X, double Y) DefaultDockedPosition = (.6, .15);

    public CardPlacementMode PlacementMode { get; init; } = CardPlacementMode.BesideCursor;
    public double DockedX { get; init; } = DefaultDockedPosition.X;
    public double DockedY { get; init; } = DefaultDockedPosition.Y;
}
