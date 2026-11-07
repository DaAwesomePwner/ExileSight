using ExileSight.Core.Features;
using ExileSight.Core.Setup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.Features;

/// <summary>Connects features to the app-owned session; never opens Client.txt.</summary>
public sealed class FeatureLogPump(FeatureHost features, ClientLogSession session,
    ILogger<FeatureLogPump> logger) : IHostedService
{
    private LogConnections? connection;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        connection = features.ConnectLogs(session.Events, (id, type) =>
            logger.LogError("Feature {FeatureId} log subscriber failed: {ExceptionType}", id, type));
        return Task.CompletedTask;
    }
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (connection is null) return;
        try { await connection.StopAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false); }
        catch (TimeoutException) { logger.LogWarning("Feature log handlers exceeded shutdown deadline"); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
}
