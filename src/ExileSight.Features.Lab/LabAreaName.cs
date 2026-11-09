namespace ExileSight.Features.Lab;

public static class LabAreaName
{
    // Keep readable casing for display; comparisons additionally ignore case.
    public static string Normalize(string name) => string.Join(" ",
        name.Replace('’', '\'').Replace('‘', '\'').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    public static bool Matches(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
}
