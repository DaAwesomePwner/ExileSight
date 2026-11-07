using ExileSight.Core.Logs;

namespace ExileSight.Core.Features;

public sealed partial class FeatureHost
{
    private readonly List<(string Owner, Action<ClientLogEvent> Handler, LogReplay? Replay)> envelopeSubscriptions = [];

    /// <summary>Each registered callback gets its own serial bounded queue on the shared session.</summary>
    public IDisposable ConnectLogs(ClientLogFeed feed, Action<string, string>? reportFailure = null)
    {
        var connections = new List<LogSubscription>();
        foreach (var (owner, handler) in subscriptions)
            connections.Add(feed.Subscribe(e => handler(e.Event), reportFailure: type => reportFailure?.Invoke(owner, type)));
        foreach (var (owner, handler, replay) in envelopeSubscriptions)
            connections.Add(feed.Subscribe(handler, replay, reportFailure: type => reportFailure?.Invoke(owner, type)));
        return new Connections(connections);
    }
    private sealed class Connections(List<LogSubscription> subscriptions) : IDisposable
    {
        public void Dispose() { foreach (var subscription in subscriptions) subscription.Dispose(); }
    }
}
