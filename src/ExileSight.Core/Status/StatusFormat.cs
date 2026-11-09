using System.Globalization;
namespace ExileSight.Core.Status;

public static class StatusFormat
{
    public static string Date(DateTimeOffset value) => value.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);
    public static string Deaths(int count) => count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " death" : " deaths");
    public static string Duration(double seconds)
    {
        var whole = (long)Math.Max(0, seconds);
        return whole >= 3600
            ? FormattableString.Invariant($"{whole / 3600}:{whole / 60 % 60:00}:{whole % 60:00}")
            : FormattableString.Invariant($"{whole / 60}:{whole % 60:00}");
    }
}

/// <summary>Passive refresh and layout changes must not destroy an active or unsaved editor.</summary>
public static class ShellRefreshPolicy
{
    public static bool ShouldRebuild(bool changed, bool layoutChanged, bool focused, string? draft, string? saved) =>
        (changed || layoutChanged) && !focused && (draft is null || string.Equals(draft, saved, StringComparison.Ordinal));
}
