namespace ExileSight.Features.Lab;

public sealed class LabTracker(LabLayout layout)
{
    public LabRoom? Current { get; private set; }
    public bool AtPlaza { get; private set; }
    public bool Uncertain { get; private set; }
    public bool Active => AtPlaza || Current is not null;
    public void Enter(string name)
    {
        if (LabAreaName.Matches(name, "Aspirants' Plaza"))
        { Current = null; AtPlaza = true; Uncertain = false; return; }
        if (!layout.Rooms.Any(r => Matches(r, name)))
        { Current = null; AtPlaza = false; Uncertain = false; return; }
        if (Current is null && Matches(layout.Rooms[0], name))
        { Current = layout.Rooms[0]; AtPlaza = false; Uncertain = false; return; }
        if (Current is null) { Uncertain = true; return; }
        var adjacent = Current.Exits.Values.Append(Current.SecretPassage ?? "").ToHashSet();
        var matches = layout.Rooms.Where(r => adjacent.Contains(r.Id) && Matches(r, name)).ToArray();
        if (matches.Length == 1) { Current = matches[0]; Uncertain = false; return; }
        Uncertain = true;
    }
    private static bool Matches(LabRoom room, string name) => LabAreaName.Matches(room.Name, name);
}
