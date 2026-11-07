using ExileSight.Core.Diagnostics;
using ExileSight.Core.Logs;
using ExileSight.Core.Logs.Tail;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.App.Logs;

public sealed class ClientLogSession(ISettingsStore<SettingsDocument> settings) : IDebugSection, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly SemaphoreSlim transition = new(1);
    private readonly Queue<string> recent = new();
    private CancellationTokenSource? cancellation;
    private Task running = Task.CompletedTask;
    private string? path;
    private string area = "Unknown", level = "None", status = "Waiting for setup";
    private bool disposed;
    public string Title => "Client log (read-only)";

    public async Task RefreshAsync()
    {
        await transition.WaitAsync();
        try
        {
            if (disposed) return;
            var next = settings.Snapshot.General.ClientTxtPath;
            if (next == path) return;
            cancellation?.Cancel();
            await running;
            cancellation?.Dispose();
            path = next;
            lock (sync) { recent.Clear(); area = "Unknown"; level = "None"; }
            if (string.IsNullOrWhiteSpace(path)) return;
            cancellation = new();
            running = ReadAsync(path, cancellation.Token);
        }
        finally { transition.Release(); }
    }

    private async Task ReadAsync(string selected, CancellationToken token)
    {
        var tail = new PollingFileTail(warning: warning => { lock (sync) status = warning; });
        var parser = new LogParser();
        try
        {
            lock (sync) status = "Following new entries";
            await foreach (var line in tail.ReadLinesAsync(selected, cancellationToken: token).ConfigureAwait(false))
            {
                var parsed = parser.Parse(line);
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
        catch (Exception) { lock (sync) status = "Log reader stopped; run setup again to select a readable log."; }
    }

    public IReadOnlyList<KeyValuePair<string, string>> Snapshot()
    {
        lock (sync) return [new("Status", status), new("Current area", area), new("Last level-up", level),
            new("Last 10 parsed events", string.Join("\n", recent))];
    }

    public async ValueTask DisposeAsync()
    {
        await transition.WaitAsync();
        try { disposed = true; cancellation?.Cancel(); await running; cancellation?.Dispose(); }
        finally { transition.Release(); }
    }
}
