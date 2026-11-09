namespace ExileSight.Features.Stashninja;

/// <summary>Controls admission to the existing display-only watch, without polling while idle.</summary>
public sealed class StashPriceSession
{
    private bool? requested;
    public bool ShouldWatch(bool automatic) => requested ?? automatic;
    public void Toggle(bool automatic) => requested = !ShouldWatch(automatic);
    public void EndVisit() => requested = null;
}
