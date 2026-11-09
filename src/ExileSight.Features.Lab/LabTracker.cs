namespace ExileSight.Features.Lab;

public sealed class LabTracker(LabLayout layout)
{
    public LabRoom? Current { get; private set; }
    public bool Uncertain { get; private set; }
    public void Enter(string name)
    {
        if (name == "Aspirants' Plaza" || Current is null && Matches(layout.Rooms[0], name))
        { Current = layout.Rooms[0]; Uncertain = false; return; }
        if (Current is null) return;
        var adjacent = Current.Exits.Values.Append(Current.SecretPassage ?? "").ToHashSet();
        var matches = layout.Rooms.Where(r => adjacent.Contains(r.Id) && Matches(r, name)).ToArray();
        if (matches.Length == 1) { Current = matches[0]; Uncertain = false; return; }
        if (matches.Length == 0 && Matches(Current, name)) { Uncertain = false; return; }
        // Unknown lab rooms retain position. Known towns/hideouts prove that the run ended.
        if (IsOutside(name)) { Current = null; Uncertain = false; return; }
        Uncertain = true;
    }
    private static bool Matches(LabRoom room, string name) => string.Equals(room.Name, name, StringComparison.OrdinalIgnoreCase);
    private static bool IsOutside(string name) => name.EndsWith("Hideout", StringComparison.OrdinalIgnoreCase) ||
        new[] { "Lioneye's Watch", "The Forest Encampment", "The Sarn Encampment", "Highgate", "Overseer's Tower",
            "The Bridge Encampment", "Oriath Docks", "Oriath", "Karui Shores", "The Coast" }.Contains(name);
}
