using ExileSight.Core.Pricing;

namespace ExileSight.Features.Vaalasync;

/// <summary>On-demand discovery uses the host's shared, persisted hourly price cache.</summary>
public sealed class ShopLeague(Func<string?> configured, Func<Task<LeagueResult>> discover)
{
    private string fallback = "Standard";
    public string Current
    {
        get
        {
            var value = configured();
            return string.IsNullOrWhiteSpace(value) ? Volatile.Read(ref fallback) : value;
        }
    }

    public async Task<string> ResolveAsync()
    {
        var value = configured();
        if (!string.IsNullOrWhiteSpace(value)) return value;
        try
        {
            var result = await discover().ConfigureAwait(false);
            // A stale seasonal name is not evidence of the current challenge league.
            Volatile.Write(ref fallback, result is { IsStale: false, Diagnostic: null, Leagues.Count: > 0 }
                ? result.DefaultLeague : "Standard");
        }
        catch (Exception)
        {
            Volatile.Write(ref fallback, "Standard");
        }
        return Current; // A setting changed while discovery was pending still wins.
    }
}
