using System.Text.Json;
using System.Collections.Concurrent;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Pricing;

public sealed record PriceResult(PriceOverview? Overview, DateTimeOffset? UpdatedAt, bool IsStale, string? Diagnostic);
public sealed record LeagueResult(IReadOnlyList<string> Leagues, string DefaultLeague, bool IsStale, string? Diagnostic);

public sealed class PriceService(IAppPaths paths, IClock clock, PriceTransport transport)
{
    private readonly ConcurrentDictionary<string, (string Json, PriceOverview Overview)> parsed = new();
    private readonly PriceDocumentCache cache = new(paths, clock, transport);
    public async Task<PriceResult> GetAsync(string league, string category, TimeSpan refreshInterval,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(league)) return new(null, null, true, "Set your league in Prices settings.");
        var result = await cache.GetAsync(PriceEndpoints.Overview(league, category),
            json => Parse(json, league, category), refreshInterval, cancellationToken).ConfigureAwait(false);
        try
        {
            return new(result.Json is null ? null : Parse(result.Json, league, category),
                result.UpdatedAt, result.IsStale, result.Diagnostic);
        }
        catch (Exception e) when (e is JsonException or OverflowException) { return new(null, result.UpdatedAt, true, "Cached prices are invalid."); }
    }

    private PriceOverview Parse(string json, string league, string category)
    {
        var key = league.ToUpperInvariant() + "/" + category;
        if (parsed.TryGetValue(key, out var entry) && entry.Json == json) return entry.Overview;
        var overview = PriceParser.Parse(json, category);
        parsed[key] = (json, overview);
        return overview;
    }

    private readonly object leagueSync = new();
    private Task<LeagueResult>? leagueRequest;

    /// <summary>A configured league always wins; otherwise prefer a current softcore challenge league.</summary>
    public async Task<LeagueResult> GetLeaguesAsync(string? configuredLeague = null, CancellationToken cancellationToken = default)
    {
        Task<LeagueResult> request;
        lock (leagueSync)
        {
            if (leagueRequest is null || leagueRequest.IsCompleted)
                leagueRequest = DiscoverLeaguesAsync(cancellationToken);
            request = leagueRequest;
        }
        var result = await request.WaitAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(configuredLeague) ? result : result with { DefaultLeague = configuredLeague };
    }

    private async Task<LeagueResult> DiscoverLeaguesAsync(CancellationToken cancellationToken)
    {
        var result = await cache.GetAsync(PriceEndpoints.Leagues, json => ParseLeagues(json),
            TimeSpan.FromHours(1), cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> leagues = [];
        try { if (result.Json is not null) leagues = ParseLeagues(result.Json); }
        catch (JsonException) { return new([], "Standard", true, "Set your league; cached league list is invalid."); }
        var preferred = leagues.FirstOrDefault(l => !l.Contains("Hardcore", StringComparison.OrdinalIgnoreCase) &&
            !l.Contains("Ruthless", StringComparison.OrdinalIgnoreCase) &&
            !l.Contains("SSF", StringComparison.OrdinalIgnoreCase) && l != "Standard");
        return new(leagues, preferred ?? "Standard",
            result.IsStale, result.Diagnostic ?? (leagues.Count == 0 ? "Set your league; league list is unavailable." : null));
    }
    private static IReadOnlyList<string> ParseLeagues(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("economyLeagues", out var entries) || entries.ValueKind != JsonValueKind.Array)
            throw new JsonException("Missing economy leagues.");
        return entries.EnumerateArray().Select(e =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                ? name.GetString() : throw new JsonException("Invalid league entry."))
            .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).Distinct(StringComparer.Ordinal).ToArray();
    }
}
