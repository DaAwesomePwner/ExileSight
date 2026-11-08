using ExileSight.Core.Pricing;

namespace ExileSight.Features.Omni;

/// <summary>Demand-driven retry; never creates input or schedules a timer.</summary>
public sealed class OmniLeagueDiscovery(Func<Task<LeagueResult>> discover, Action<string> report,
    Func<long>? milliseconds = null)
{
    public const long RetryMilliseconds = 10 * 60 * 1000;
    private readonly object sync = new();
    private readonly Func<long> now = milliseconds ?? (() => Environment.TickCount64);
    private bool running, succeeded;
    private long? attemptedAt;
    private LeagueResult? result;
    public LeagueResult? Result { get { lock (sync) return result; } }
    public Task RefreshAsync()
    {
        lock (sync)
        {
            var timestamp = now();
            if (running || succeeded || (attemptedAt is { } last && timestamp - last < RetryMilliseconds))
                return Task.CompletedTask;
            running = true;
            attemptedAt = timestamp;
        }
        return DiscoverAsync();
    }
    private async Task DiscoverAsync()
    {
        try
        {
            var discovered = await discover().ConfigureAwait(false);
            lock (sync)
            {
                result = discovered;
                succeeded = !discovered.IsStale && discovered.Leagues.Count > 0 && discovered.Diagnostic is null;
            }
            if (discovered.IsStale || discovered.Leagues.Count == 0 || discovered.Diagnostic is not null)
                Report(nameof(InvalidOperationException));
        }
        catch (Exception error) { Report(error.GetType().Name); }
        finally { lock (sync) running = false; }
    }
    private void Report(string type)
    {
        try { report(type); }
        catch (Exception) { /* Diagnostics must not prevent a later retry. */ }
    }
}
