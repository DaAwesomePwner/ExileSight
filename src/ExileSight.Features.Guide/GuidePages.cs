using ExileSight.Core.DataPacks;

namespace ExileSight.Features.Guide;

internal static class GuidePages
{
    internal static GuidePageData[] Select(GuideData data, bool leagueStart, string bandit) =>
        data.Pages.Where(p => p.Condition is null || p.Condition.Kind switch
        {
            "league-start" => p.Condition.Values.Contains(leagueStart ? "yes" : "no"),
            "bandit" => p.Condition.Values.Contains(bandit), _ => false
        }).ToArray();
}
