using ExileSight.Core.Features;
using ExileSight.Core.Logs;
using ExileSight.Core.Settings;
using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Windows.Install;
using Microsoft.Extensions.Hosting;

namespace ExileSight.App.Features;

/// <summary>Passive feature event feed. No token issuer is available to log subscribers.</summary>
public sealed class FeatureLogPump(FeatureHost features, ISettingsStore<SettingsDocument> settings) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var path = settings.Snapshot.General.ClientTxtPath;
            if (!ClientLogDiscovery.IsLocalAbsolutePath(path))
            { await Task.Delay(500, stoppingToken); continue; }
            using var session = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            void Changed(object? sender, SettingsChangedEventArgs args)
            { if (settings.Snapshot.General.ClientTxtPath != path) session.Cancel(); }
            settings.Changed += Changed;
            try
            {
                if (settings.Snapshot.General.ClientTxtPath != path) continue;
                var stream = new LogEventStream(new LocalLogTail(new LocalPathResolver()), new LogParser());
                await foreach (var value in stream.ReadAsync(path!, cancellationToken: session.Token)) features.Publish(value);
            }
            catch (OperationCanceledException) when (session.IsCancellationRequested) { }
            finally { settings.Changed -= Changed; }
        }
    }
}
