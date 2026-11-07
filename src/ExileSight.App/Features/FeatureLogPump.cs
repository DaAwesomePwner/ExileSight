using ExileSight.Core.Features;
using ExileSight.Core.Logs;
using ExileSight.Core.Settings;
using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Windows.Install;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.Features;

/// <summary>Passive feature event feed. No token issuer is available to log subscribers.</summary>
public sealed class FeatureLogPump(FeatureHost features, ISettingsStore<SettingsDocument> settings,
    ILogger<FeatureLogPump> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!features.HasLogSubscriptions) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var snapshot = settings.Snapshot;
            var path = snapshot.General.ClientTxtPath;
            if (!snapshot.General.FirstRunCompleted || !ClientLogDiscovery.IsLocalAbsolutePath(path))
            { await Task.Delay(500, stoppingToken); continue; }
            using var session = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var sync = new object();
            var closed = false;
            void Changed(object? sender, SettingsChangedEventArgs args)
            {
                lock (sync)
                {
                    if (closed) return; // An event invocation may already have captured this delegate.
                    var current = settings.Snapshot.General;
                    if (current.ClientTxtPath != path || !current.FirstRunCompleted) session.Cancel();
                }
            }
            settings.Changed += Changed;
            try
            {
                Changed(null, new("features"));
                var stream = new LogEventStream(new LocalLogTail(new LocalPathResolver()), new LogParser());
                await foreach (var value in stream.ReadAsync(path!, cancellationToken: session.Token))
                    features.Publish(value, (id, error) => logger.LogError("Feature {FeatureId} log subscriber failed: {ExceptionType}", id, error.GetType().Name));
            }
            catch (OperationCanceledException) when (session.IsCancellationRequested) { }
            catch (Exception error)
            {
                logger.LogError("Feature log reader failed; retrying: {ExceptionType}", error.GetType().Name);
                await Task.Delay(1000, stoppingToken);
            }
            finally
            {
                lock (sync) closed = true;
                settings.Changed -= Changed;
            }
        }
    }
}
