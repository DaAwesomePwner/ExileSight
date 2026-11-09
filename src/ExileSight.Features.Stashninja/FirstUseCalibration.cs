namespace ExileSight.Features.Stashninja;

/// <summary>Transient evidence only; persistence requires two consecutive matching identifications.</summary>
public sealed class FirstUseCalibration
{
    private Layout? page;
    private Resolution? size;
    private Transform? previous;
    public void Reset() { page = null; size = null; previous = null; }
    public Transform? Pending(Layout layout, Resolution resolution) => page == layout && size == resolution ? previous : null;

    public bool Observe(Layout? layout, Resolution resolution, Transform? transform)
    {
        if (layout is null || transform?.IsValid != true) { Reset(); return false; }
        var confirmed = page == layout && size == resolution && previous is not null && layout.Cells.All(cell =>
        {
            var center = Geometry.Center(layout, cell);
            var a = previous.Apply(center); var b = transform.Apply(center);
            return Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1;
        });
        page = layout; size = resolution; previous = transform;
        return confirmed;
    }
}
