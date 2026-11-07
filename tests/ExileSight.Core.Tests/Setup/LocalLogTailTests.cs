using System.Threading.Channels;
using ExileSight.Core.Logs.Tail;
using ExileSight.Core.Settings;
using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Files;
using ExileSight.Platform.Abstractions.Install;
using Xunit;

namespace ExileSight.Core.Tests.Setup;

public sealed class LocalLogTailTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingDriveCanBecomeLocalButNeverOpensWhenRemote(bool becomesLocal)
    {
        var root = Path.Combine(Path.GetTempPath(), "ExileSight-gate-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var log = Path.Combine(root, "Client.txt");
            await File.WriteAllTextAsync(log, "");
            using var settings = new JsonSettingsStore<SettingsDocument>(new AppPaths(root),
                Path.Combine(root, "settings.json"), SettingsJsonContext.Default.SettingsDocument, () => new());
            settings.Update("general", s => s.General.ClientTxtPath = log);
            var resolver = new ChangingDrive();
            var timer = new ControlledTimer();
            var identity = new OpenCounter();
            await using var session = new ClientLogSession(settings, new LocalLogTail(resolver, timer, identity));
            await session.RefreshAsync();
            var tick = await timer.Drained();
            Assert.Contains("Drive not available", Rows(session)["Status"]);
            Assert.Equal(0, identity.Opens);
            Assert.Equal(0, resolver.Inspections);

            resolver.Type = becomesLocal ? 3 : 4;
            tick.SetResult();
            tick = await timer.Drained();
            if (becomesLocal)
            {
                Assert.Equal(1, identity.Opens);
                await File.AppendAllTextAsync(log, ": You have entered Local.\n");
                tick.SetResult();
                tick = await timer.Drained();
                Assert.Equal("Local", Rows(session)["Current area"]);
            }
            else Assert.Contains("Network", Rows(session)["Status"]);

            var opensBeforeRemote = identity.Opens;
            resolver.Type = 4;
            await File.AppendAllTextAsync(log, ": You have entered Remote.\n");
            for (var i = 0; i < 3; i++)
            {
                tick.SetResult();
                tick = await timer.Drained();
                Assert.Equal(opensBeforeRemote, identity.Opens);
                Assert.Contains("Network", Rows(session)["Status"]);
            }
            Assert.DoesNotContain("Remote", Rows(session)["Last 10 parsed events"]);
        }
        finally { Directory.Delete(root, true); }
    }

    private static Dictionary<string, string> Rows(ClientLogSession session) => session.Snapshot().ToDictionary();
    private sealed class ChangingDrive : ILocalPathResolver, IPathComponentProbe
    {
        public int Type = 1;
        public int Inspections;
        public int DriveType(string root) => Type;
        public bool IsRemoteDrive(string path) => Type == 4;
        public string? Rejection(string path) => new CheckedLocalPathResolver(this).Rejection(@"E:\logs\Client.txt");
        public string? FinalPath(string path) => Rejection(path) is null ? path : null;
        public PathComponent Inspect(string path) { Inspections++; return new(PathComponentKind.Ordinary); }
    }
    private sealed class OpenCounter : IFileTailIdentityProvider
    {
        public int Opens;
        public FileTailIdentity Read(FileStream file) { Opens++; return new(default, false); }
    }
    private sealed class ControlledTimer : IFileTailTimer
    {
        private readonly Channel<TaskCompletionSource> drains = Channel.CreateUnbounded<TaskCompletionSource>();
        public async Task DelayAsync(int milliseconds, CancellationToken token)
        {
            var tick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await drains.Writer.WriteAsync(tick, token);
            await tick.Task.WaitAsync(token);
        }
        public async Task<TaskCompletionSource> Drained() =>
            await drains.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
    }
}
