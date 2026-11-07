using System.Threading.Channels;
using ExileSight.Core.Logs.Tail;
using ExileSight.Core.Settings;
using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Files;
using ExileSight.Platform.Abstractions.Install;
using Xunit;

namespace ExileSight.Core.Tests.Setup;

public sealed class ClientLogSessionTests
{
    [Fact]
    public async Task TailsFromEndRestartsAndKeepsTenPrivacySafeEventsThenDisposes()
    {
        var root = Path.Combine(Path.GetTempPath(), "ExileSight-session-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var first = Path.Combine(root, "Client.txt");
            var second = Path.Combine(root, "LatestClient.txt");
            await File.WriteAllTextAsync(first, ": You have entered Historical.\n");
            await File.WriteAllTextAsync(second, ": You have entered OldSecond.\n");
            using var settings = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), Path.Combine(root, "settings.json"),
                SettingsJsonContext.Default.SettingsDocument, () => new());
            settings.Update("general", s => s.General.ClientTxtPath = first);
            var timer = new ControlledTimer();
            await using var session = new ClientLogSession(settings, new PollingFileTail(timer));
            await session.RefreshAsync();
            var tick = await timer.Drained();
            Assert.Equal("Unknown", Rows(session)["Current area"]); // Historical lines were skipped.
            await File.AppendAllTextAsync(first, string.Concat(Enumerable.Range(0, 12).Select(i => $": You have entered Area{i}.\n")) +
                "@From PrivateSender: secret chat body\n: PrivateCharacter (Witch) is now level 23\n");
            tick.SetResult();
            tick = await timer.Drained();
            var rows = Rows(session);
            Assert.Equal("Area11", rows["Current area"]);
            Assert.Contains("23", rows["Last level-up"]);
            Assert.Equal(10, rows["Last 10 parsed events"].Split('\n').Length);
            Assert.Contains("ChatMessage", rows["Last 10 parsed events"]);
            var all = string.Join("\n", rows.Values);
            Assert.DoesNotContain("Private", all);
            Assert.DoesNotContain("secret chat body", all);
            settings.Update("general", s => s.General.ClientTxtPath = second);
            await session.RefreshAsync(); // Cancels the first reader while its timer is waiting.
            tick = await timer.Drained();
            Assert.Equal("Unknown", Rows(session)["Current area"]);
            await File.AppendAllTextAsync(first, ": You have entered WrongFile.\n");
            await File.AppendAllTextAsync(second, ": You have entered NewSecond.\n");
            tick.SetResult();
            await timer.Drained();
            Assert.Equal("NewSecond", Rows(session)["Current area"]);
            await session.DisposeAsync();
            await session.DisposeAsync(); // Idempotent and no reader left waiting.
            await File.AppendAllTextAsync(second, ": You have entered AfterDisposal.\n");
            Assert.Equal("NewSecond", Rows(session)["Current area"]);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MissingLocalLogRetriesWithResolverAndStartsWhenCreated()
    {
        var root = Path.Combine(Path.GetTempPath(), "ExileSight-missing-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var log = Path.Combine(root, "Client.txt");
            var resolver = new LocalResolver();
            Assert.Contains("does not exist", ClientLogDiscovery.Validate(log, resolver));
            using var settings = new JsonSettingsStore<SettingsDocument>(new AppPaths(root),
                Path.Combine(root, "settings.json"), SettingsJsonContext.Default.SettingsDocument, () => new());
            settings.Update("general", s => { s.General.ClientTxtPath = log; s.General.FirstRunCompleted = true; });
            var timer = new ControlledTimer();
            await using var session = new ClientLogSession(settings, pathResolver: resolver, pollingTimer: timer);
            await session.RefreshAsync();
            var tick = await timer.Drained();
            Assert.Contains("retrying", Rows(session)["Status"]);
            await File.WriteAllTextAsync(log, "");
            tick.SetResult();
            tick = await timer.Drained();
            Assert.Null(ClientLogDiscovery.Validate(log, resolver));
            await File.AppendAllTextAsync(log, ": You have entered Recovered.\n");
            tick.SetResult();
            await timer.Drained();
            Assert.Equal("Recovered", Rows(session)["Current area"]);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class LocalResolver : ILocalPathResolver
    {
        public bool IsRemoteDrive(string path) => false;
        public string? FinalPath(string path) => path; // Missing descendants of the checked local ancestor.
    }

    private static Dictionary<string, string> Rows(ClientLogSession session) => session.Snapshot().ToDictionary();

    private sealed class ControlledTimer : IFileTailTimer
    {
        private readonly Channel<TaskCompletionSource> drains = Channel.CreateUnbounded<TaskCompletionSource>();
        public async Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
        {
            var tick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await drains.Writer.WriteAsync(tick, cancellationToken);
            await tick.Task.WaitAsync(cancellationToken);
        }
        public async Task<TaskCompletionSource> Drained() => await drains.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
