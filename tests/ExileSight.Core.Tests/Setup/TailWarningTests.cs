using ExileSight.Platform.Abstractions.Files;

using ExileSight.Core.Logs.Tail;
using ExileSight.Core.Settings;
using ExileSight.Core.Setup;
using Xunit;

namespace ExileSight.Core.Tests.Setup;

public sealed class TailWarningTests
{
    [Fact]
    public async Task InjectedTailWarningsReachStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), "ExileSight-warning-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            using var settings = new JsonSettingsStore<SettingsDocument>(new AppPaths(root),
                Path.Combine(root, "settings.json"), SettingsJsonContext.Default.SettingsDocument, () => new());
            settings.Update("general", s => s.General.ClientTxtPath = Path.Combine(root, "Client.txt"));
            var timer = new WaitingTimer();
            await using var session = new ClientLogSession(settings, new PollingFileTail(timer));
            await session.RefreshAsync();
            await timer.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal("Log temporarily unavailable; retrying.", session.Snapshot().ToDictionary()["Status"]);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class WaitingTimer : IFileTailTimer
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
}
