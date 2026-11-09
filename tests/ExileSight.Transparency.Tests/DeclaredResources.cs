using System.Text.RegularExpressions;
using ExileSight.Core.DataPacks.Loading;
using ExileSight.Core.Pricing;
using ExileSight.Core.Updates;

namespace ExileSight.Transparency.Tests;

internal static class DeclaredResources
{
    // No production file-write contract exists. Audited against feature stores and App/Features registration.
    // Every feature must have an explicit entry; new features cannot silently claim no writes.
    internal static readonly IReadOnlyDictionary<string, string[]> FeatureWrites = new Dictionary<string, string[]>
    {
        ["events"] = ["active profile JSON (event settings)"],
        ["gearcmp"] = ["poe1/gearcmp/active.json", "poe1/gearcmp/default.json", "poe1/gearcmp/character-{hash}.json"],
        ["guide"] = ["poe1/guide/progress.json", "poe1/guide/{hash}.json", "user-selected CSV export"],
        ["lab"] = ["poe1/lab/{difficulty}.json", "poe1/lab/latest.txt"],
        ["iteminfo"] = ["active profile JSON (highlight profiles)"],
        ["macros"] = ["active profile JSON (commands)"],
        ["mapinfo"] = ["poe1/mapinfo/ratings.json"],
        ["maptracker"] = ["active profile JSON (league)", "poe1/maptracker/{league-hash}.jsonl", "user-selected CSV export"],
        ["omni"] = [],
        ["searchstr"] = [],
        ["stashninja"] = ["poe1/stashninja/calibration.json"],
        ["timers"] = ["poe1/timers/timers.json"],
        ["vaalasync"] = ["poe1/vaalasync/history.json"]
    };

    internal sealed record Service(string Name, string[] Hosts, string[] Writes);

    internal static IEnumerable<Service> Services(string root)
    {
        // Host names come from the single production policy sources, then pass the actual policies.
        var packPolicy = Path.Combine(root, "src/ExileSight.Core/DataPacks/Loading/GitHubTransport.cs");
        var updatePolicy = Path.Combine(root, "src/ExileSight.Core/Updates/UpdateNetworkPolicy.cs");
        var candidates = Regex.Matches(File.ReadAllText(packPolicy) + File.ReadAllText(updatePolicy),
                "\"([a-z0-9.-]+\\.[a-z]+)\"").Select(m => m.Groups[1].Value).Distinct().ToArray();
        yield return new("Data packs", candidates.Where(h => GitHubTransport.IsAllowed(new("https://" + h))).ToArray(),
            ["cache/poe1/packs/*.pack.json", "cache/poe1/packs/*.refresh.json"]);
        yield return new("Prices", [PriceEndpoints.Leagues.Host], ["cache/poe1/poeninja/*.json", "cache/poe1/poeninja/*.json.lock"]);
        yield return new("Updates (installed builds)", candidates.Where(h => UpdateNetworkPolicy.IsAllowed(new("https://" + h))).ToArray(),
            ["Velopack-managed update packages and install files"]);
        // Remaining file behaviour has no central production declaration; keep this small fallback together.
        yield return new("Settings and profiles", [], ["poe1/profiles.json", "poe1/{profile-id}.json"]);
        yield return new("Action Gate audit", [], ["logs/actions.log"]);
    }
}
