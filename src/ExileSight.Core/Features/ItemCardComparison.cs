namespace ExileSight.Core.Features;

public enum DeltaDirection { Unchanged, Increase, Decrease }
public sealed record ItemCardDelta(string Stat, DeltaDirection Direction, decimal AbsoluteValue);
public sealed record ItemCardCompareSlot(string Slot, bool NothingEquipped, IReadOnlyList<ItemCardDelta> Deltas);
/// <summary>Passive comparison snapshot. Note is presented once in muted text.</summary>
public sealed record ItemCardComparison(IReadOnlyList<ItemCardCompareSlot> Slots, string Note);
