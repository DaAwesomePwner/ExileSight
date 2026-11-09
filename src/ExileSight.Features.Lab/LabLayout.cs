namespace ExileSight.Features.Lab;

public sealed record LabRoom(string Id, string Name, string AreaCode, double X, double Y,
    IReadOnlyList<string> Contents, IReadOnlyDictionary<string, string> Exits, string? SecretPassage);
public sealed record LabLayout(string Difficulty, DateOnly Date, string Weapon, string Phase1, string Phase2,
    string Trap1, string Trap2, IReadOnlyList<string> SectionLayouts, IReadOnlyList<LabRoom> Rooms)
{
    // Assumption: the daily Labyrinth reset is midnight UTC.
    public static readonly TimeSpan ResetUtc = TimeSpan.Zero;
    public bool IsOutdated(DateTimeOffset now) => Date != DateOnly.FromDateTime(now.UtcDateTime - ResetUtc);
}
