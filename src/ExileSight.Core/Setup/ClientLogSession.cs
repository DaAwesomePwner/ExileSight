using ExileSight.Core.Diagnostics;
using ExileSight.Platform.Abstractions.Files;
using ExileSight.Platform.Abstractions.Install;
using ExileSight.Core.Logs;
using ExileSight.Core.Logs.Tail;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.Core.Setup;

public sealed class ClientLogSession(ISettingsStore<SettingsDocument> settings, ExileSight.Platform.Abstractions.Files.IFileTail? fileTail = null, ILocalPathResolver? pathResolver = null, IFileTailTimer? pollingTimer = null) : IDebugSection, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly SemaphoreSlim transition = new(1);
    private readonly Queue<string> recent = new();
    private CancellationTokenSource? cancellation;
    private Task running = Task.CompletedTask;
    private string? path;
    private string area = "Unknown", level = "None", status = "Waiting for setup";
    private bool disposed;
    public ClientLogFeed Events { get; } = new();
    public string Title => "Client log (read-only)";

    public async Task RefreshAsync()
    {
        await transition.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            var next = settings.Snapshot.General.ClientTxtPath;
            if (next == path) return;
            cancellation?.Cancel();
            await running.ConfigureAwait(false);
            cancellation?.Dispose();
            path = next;
            Events.ChangePath();
            lock (sync) { recent.Clear(); area = "Unknown"; level = "None"; }
            if (!ClientLogDiscovery.IsLocalAbsolutePath(path))
            { lock (sync) status = "Choose a fully-qualified local log path in setup."; return; }
            cancellation = new();
            running = FollowAsync(path!, cancellation.Token);
        }
        finally { transition.Release(); }
    }

    private async Task FollowAsync(string selected, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await ReadAsync(selected, token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                lock (sync) status = $"Log reader failed: {error.GetType().Name}; retrying.";
            }
            try
            {
                if (pollingTimer is not null) await pollingTimer.DelayAsync(1000, token).ConfigureAwait(false);
                else await Task.Delay(1000, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        }
    }

    private async Task ReadAsync(string selected, CancellationToken token)
    {
        var tail = fileTail ?? new LocalLogTail(pathResolver, pollingTimer);
        var diagnostics = tail as IFileTailDiagnostics;
        void Report(FileTailDiagnostic warning) { lock (sync) status = warning.Message; }
        if (diagnostics is not null) diagnostics.Diagnostic += Report;
        var parser = new LogParser();
        try
        {
            lock (sync) status = "Following new entries";
            await foreach (var line in tail.ReadLinesAsync(selected, cancellationToken: token).ConfigureAwait(false))
            {
                var parsed = parser.Parse(line);
                Events.Publish(parsed);
                if (parsed is Unknown) continue;
                lock (sync)
                {
                    if (parsed is AreaEntered entered) area = entered.Name;
                    if (parsed is CharacterLevelUp up) level = $"Level {up.Level} (identity omitted; may include party members)";
                    recent.Enqueue(parsed switch
                    {
                        AreaEntered entry => "Area entered: " + entry.Name,
                        CharacterLevelUp entry => "Level-up: " + entry.Level,
                        _ => parsed.GetType().Name
                    });
                    while (recent.Count > 10) recent.Dequeue();
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { if (diagnostics is not null) diagnostics.Diagnostic -= Report; }
    }

    public IReadOnlyList<KeyValuePair<string, string>> Snapshot()
    {
        lock (sync) return [new("Status", status), new("Current area", area), new("Last level-up", level),
            new("Event delivery", Events.Diagnostic), new("Last 10 parsed events", string.Join("\n", recent))];
    }

    public async ValueTask DisposeAsync()
    {
        await transition.WaitAsync().ConfigureAwait(false);
        try { if (disposed) return; disposed = true; cancellation?.Cancel(); await running.ConfigureAwait(false); cancellation?.Dispose(); Events.Dispose(); }
        finally { transition.Release(); }
    }
}
