using ExileSight.Core.Pricing;

namespace ExileSight.Features.Vaalasync;

/// <summary>On-demand discovery uses the host's shared, persisted hourly price cache.</summary>
public sealed class ShopLeague(Func<string?> configured, Func<Task<LeagueResult>> discover)
{
    private readonly object sync = new();
    private Task<LeagueResult>? pending;
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
        Task<LeagueResult> request;
        lock (sync)
        {
            if (pending is null || pending.IsCompleted) pending = discover();
            request = pending;
        }
        var result = await request.ConfigureAwait(false);
        if (result.Leagues.Count > 0) Volatile.Write(ref fallback, result.DefaultLeague);
        if (result.Diagnostic is not null)
            throw new InvalidOperationException("League discovery unavailable; use the best known league.");
        return Current; // A setting changed while discovery was pending still wins.
    }
}
