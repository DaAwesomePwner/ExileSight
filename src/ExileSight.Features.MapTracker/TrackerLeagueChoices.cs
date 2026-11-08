using ExileSight.Core.Pricing;

namespace ExileSight.Features.MapTracker;

/// <summary>Only verified index-state leagues are current; manual/saved names remain usable offline.</summary>
public sealed record TrackerLeagueChoices(string[] Leagues, string Text, string Note)
{
    public static TrackerLeagueChoices Build(LeagueResult? result, string savedLeague, string currentText)
    {
        var available = result is { IsStale: false, Diagnostic: null, Leagues.Count: > 0 };
        IEnumerable<string> names = available
            ? result!.Leagues.OrderBy(name => name == result.DefaultLeague ? 0 : 1)
            : ["Standard", "Hardcore"];
        if (!string.IsNullOrWhiteSpace(savedLeague)) names = names.Append(savedLeague);
        return new(names.Distinct(StringComparer.Ordinal).ToArray(), currentText,
            available ? "" : "Current leagues unavailable or stale (poe.ninja). Saved and permanent leagues are available; you can type a league.");
    }
}
