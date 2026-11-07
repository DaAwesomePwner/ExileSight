using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Logs;
using ExileSight.Core.Settings;
using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Files;
using Xunit;

namespace ExileSight.Core.Tests.Logs;

public sealed class ClientLogFeedTests
{
    [Fact]
    public async Task OrderedIsolatedDeliveryAndLateSubscribersShareOneTailAcrossPaths()
    {
        await using var rig = new Rig();
        var received = Channel.CreateUnbounded<ClientLogEvent>();
        var errors = Channel.CreateUnbounded<string>();
        var host = new FeatureHost([new Module(c =>
        {
            c.Subscribe<AreaEntered>(_ => throw new InvalidOperationException("private message"));
            c.SubscribeLog(e => received.Writer.TryWrite(e));
        })], (_, _) => null, (_, _, _) => { });
        using var connection = host.ConnectLogs(rig.Session.Events, (_, type) => errors.Writer.TryWrite(type));
        await rig.Session.RefreshAsync();
        for (var i = 0; i < 3; i++) rig.Send(i);
        var events = new List<ClientLogEvent>();
        for (var i = 0; i < 3; i++) events.Add(await Next(received));
        Assert.Equal(new long[] { 1, 2, 3 }, events.Select(e => e.Offset));
        Assert.All(events, e => Assert.False(e.IsReplay));
        for (var i = 0; i < 3; i++) Assert.Equal("InvalidOperationException", await Next(errors));
        var late = Channel.CreateUnbounded<ClientLogEvent>();
        using var sub = rig.Session.Events.Subscribe(e => late.Writer.TryWrite(e));
        rig.Settings.Update("general", s => s.General.ClientTxtPath = Path.Combine(rig.Root, "Other.txt"));
        await rig.Session.RefreshAsync();
        rig.Send(4);
        var next = await Next(received);
        Assert.True(next.Generation > events[0].Generation);
        Assert.Equal(next, await Next(late));
        Assert.Equal(2, rig.Tail.Opens);
        Assert.Equal(1, rig.Tail.MaximumActive);
        Assert.DoesNotContain("private message", rig.Session.Events.Diagnostic);
    }

    [Fact]
    public async Task BlockedSubscriberDropsNewestWithoutBlockingTailOrOtherSubscribers()
    {
        await using var rig = new Rig();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var received = Channel.CreateUnbounded<ClientLogEvent>();
        var slowValues = new ConcurrentQueue<long>();
        using var slow = rig.Session.Events.Subscribe(e =>
        {
            slowValues.Enqueue(e.Offset);
            if (e.Offset == 1) { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); }
        }, capacity: 2);
        using var fast = rig.Session.Events.Subscribe(e => received.Writer.TryWrite(e));
        try
        {
            await rig.Session.RefreshAsync();
            rig.Send(0);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            for (var i = 1; i < 6; i++) rig.Send(i);
            for (var i = 0; i < 6; i++) await Next(received);
            Assert.Equal(3, slow.DroppedEvents);
            Assert.Contains("dropped 3 events", rig.Session.Events.Diagnostic);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task ReplayIsBoundedMarkedFilteredAndClearedOnPathChange()
    {
        await using var rig = new Rig();
        var live = Channel.CreateUnbounded<ClientLogEvent>();
        using var witness = rig.Session.Events.Subscribe(e => live.Writer.TryWrite(e));
        await rig.Session.RefreshAsync();
        for (var i = 0; i < 5; i++) { rig.Send(i); await Next(live); }
        var replayed = Channel.CreateUnbounded<ClientLogEvent>();
        using var replay = rig.Session.Events.Subscribe(e => replayed.Writer.TryWrite(e), new(FromOffset: 2, MaxEvents: 2));
        var a = await Next(replayed); var b = await Next(replayed);
        Assert.Equal(4, a.Offset); Assert.Equal(5, b.Offset);
        Assert.True(a.IsReplay); Assert.True(b.IsReplay);
        Assert.NotNull(a.Timestamp); Assert.Equal(123, a.UptimeMilliseconds);
        rig.Send(5);
        Assert.False((await Next(replayed)).IsReplay);
        var timed = Channel.CreateUnbounded<ClientLogEvent>();
        using var timeReplay = rig.Session.Events.Subscribe(e => timed.Writer.TryWrite(e),
            new(Since: new DateTime(2026, 11, 7, 12, 0, 4)));
        Assert.Equal(5, (await Next(timed)).Offset);
        Assert.Equal(6, (await Next(timed)).Offset);
        rig.Settings.Update("general", s => s.General.ClientTxtPath = Path.Combine(rig.Root, "Other.txt"));
        await rig.Session.RefreshAsync();
        var cleared = Channel.CreateUnbounded<ClientLogEvent>();
        using var newReplay = rig.Session.Events.Subscribe(e => cleared.Writer.TryWrite(e), new());
        rig.Send(6);
        var last = await Next(cleared);
        Assert.Equal(7, last.Offset); Assert.False(last.IsReplay);
        Assert.Equal(1, rig.Tail.MaximumActive);
    }

    private static async Task<T> Next<T>(Channel<T> channel) =>
        await channel.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    private sealed class Module(Action<IFeatureContext> register) : IFeature
    {
        public FeatureManifest Manifest { get; } = new("feedtest", [Game.PoE1], [], [], [], "Feed test");
        public void Register(IFeatureContext context) => register(context);
    }
    private sealed class Rig : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "feed-" + Guid.NewGuid());
        public JsonSettingsStore<SettingsDocument> Settings { get; }
        public Tail Tail { get; } = new();
        public ClientLogSession Session { get; }
        public Rig()
        {
            Directory.CreateDirectory(Root);
            Settings = new(new AppPaths(Root), Path.Combine(Root, "settings.json"), SettingsJsonContext.Default.SettingsDocument, () => new());
            Settings.Update("general", s => s.General.ClientTxtPath = Path.Combine(Root, "Client.txt"));
            Session = new(Settings, Tail);
        }
        public void Send(int i) => Tail.Lines.Writer.TryWrite($"2026/11/07 12:00:{i:00} 123 abc [INFO Client 1] : You have entered Area{i}.");
        public async ValueTask DisposeAsync()
        {
            await Session.DisposeAsync(); Settings.Dispose(); Directory.Delete(Root, true);
        }
    }
    private sealed class Tail : IFileTail
    {
        public Channel<string> Lines { get; } = Channel.CreateUnbounded<string>();
        public int Opens, MaximumActive;
        private int active;
        public async IAsyncEnumerable<string> ReadLinesAsync(string path, long? position = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Assert.Null(position);
            Opens++; MaximumActive = Math.Max(MaximumActive, ++active);
            try { await foreach (var line in Lines.Reader.ReadAllAsync(cancellationToken)) yield return line; }
            finally { active--; }
        }
    }
}
