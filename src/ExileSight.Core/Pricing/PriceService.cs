using System.Text.Json;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Pricing;

public sealed record PriceResult(PriceOverview? Overview, DateTimeOffset? UpdatedAt, bool IsStale, string? Diagnostic);
public sealed record LeagueResult(IReadOnlyList<string> Leagues, string DefaultLeague, bool IsStale, string? Diagnostic);

public sealed class PriceService(IAppPaths paths, IClock clock, PriceTransport transport)
{
    private readonly PriceDocumentCache cache = new(paths, clock, transport);
    public async Task<PriceResult> GetAsync(string league, string category, TimeSpan refreshInterval,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(league)) return new(null, null, true, "Set your league in Prices settings.");
        var result = await cache.GetAsync(PriceEndpoints.Overview(league, category),
            json => PriceParser.Parse(json, category), refreshInterval, cancellationToken).ConfigureAwait(false);
        try
        {
            return new(result.Json is null ? null : PriceParser.Parse(result.Json, category),
                result.UpdatedAt, result.IsStale, result.Diagnostic);
        }
        catch (JsonException) { return new(null, result.UpdatedAt, true, "Cached prices are invalid."); }
    }

    /// <summary>A configured league always wins; otherwise prefer a current softcore challenge league.</summary>
    public async Task<LeagueResult> GetLeaguesAsync(string? configuredLeague = null, CancellationToken cancellationToken = default)
    {
        var result = await cache.GetAsync(PriceEndpoints.Leagues, json => ParseLeagues(json),
            TimeSpan.FromHours(1), cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> leagues = [];
        try { if (result.Json is not null) leagues = ParseLeagues(result.Json); }
        catch (JsonException) { return new([], configuredLeague ?? "Standard", true, "Set your league; cached league list is invalid."); }
        var preferred = leagues.FirstOrDefault(l => !l.Contains("Hardcore", StringComparison.OrdinalIgnoreCase) &&
            !l.Contains("SSF", StringComparison.OrdinalIgnoreCase) && l != "Standard");
        return new(leagues, string.IsNullOrWhiteSpace(configuredLeague) ? preferred ?? "Standard" : configuredLeague,
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
