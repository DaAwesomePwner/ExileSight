using ExileSight.Core.Pricing;

namespace ExileSight.Features.Omni;

public static class OmniLeague
{
    // Use the price service's selected default verbatim; do not duplicate challenge-league heuristics.
    public static string Select(string? configured, LeagueResult? discovered = null) =>
        !string.IsNullOrWhiteSpace(configured) ? configured :
        !string.IsNullOrWhiteSpace(discovered?.DefaultLeague) ? discovered.DefaultLeague : "Standard";
}
