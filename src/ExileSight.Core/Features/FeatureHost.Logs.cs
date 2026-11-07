using ExileSight.Core.Logs;

namespace ExileSight.Core.Features;

public sealed partial class FeatureHost
{
    private sealed record LogRegistration(string Owner, Action<ClientLogEvent> Handler, LogReplay? Replay);
    private readonly List<LogRegistration> logRegistrations = [];

    /// <summary>One serial queue per owner; callbacks run in registration order, with independent failure isolation.</summary>
    public LogConnections ConnectLogs(ClientLogFeed feed, Action<string, string>? reportFailure = null)
    {
        var connections = new List<LogSubscription>();
        try
        {
            foreach (var group in logRegistrations.GroupBy(r => r.Owner))
            {
                var registrations = group.ToArray();
                var requests = registrations.Where(r => r.Replay is not null).Select(r => r.Replay!).ToArray();
                // Replay the union, then apply each handler's own selection and count.
                var replay = requests.Length == 0 ? null : new LogReplay(MaxEvents: int.MaxValue);
                connections.Add(feed.SubscribeCore(retained =>
                {
                    var selected = registrations.Select(r => retained.Where(e => r.Replay is { } request &&
                        (request.FromOffset is null || e.Offset >= request.FromOffset) &&
                        (request.Since is null || e.Timestamp >= request.Since))
                        .TakeLast(r.Replay?.MaxEvents ?? 0).Select(e => e.Offset).ToHashSet()).ToArray();
                    return value =>
                    {
                        List<Exception> failures = [];
                        for (var i = 0; i < registrations.Length; i++)
                        {
                            if (value.IsReplay && !selected[i].Contains(value.Offset)) continue;
                            try { registrations[i].Handler(value); }
                            catch (Exception error) { failures.Add(error); }
                        }
                        if (failures.Count > 0) throw new LogHandlerFailures(failures);
                    };
                }, replay, 256, type => reportFailure?.Invoke(group.Key, type), group.Key));
            }
            return new(connections);
        }
        catch { foreach (var connection in connections) connection.Dispose(); throw; }
    }
}

public sealed class LogConnections(List<LogSubscription> subscriptions) : IDisposable
{
    public void Dispose() { foreach (var subscription in subscriptions) subscription.Dispose(); }
    public async Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        Dispose();
        await Task.WhenAll(subscriptions.Select(s => s.Completion)).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
    }
}
