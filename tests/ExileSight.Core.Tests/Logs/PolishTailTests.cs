using ExileSight.Core.Logs;
using ExileSight.Core.Logs.Tail;
using ExileSight.Platform.Abstractions.Files;
using Xunit;

namespace ExileSight.Core.Tests.Logs;

public sealed class PolishTailTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "exilesight-polish-" + Guid.NewGuid());
    public PolishTailTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);

    [Fact]
    public async Task WarningsAreObservableThrottledAndPermissionErrorsRecover()
    {
        var path = Path.Combine(directory, "Client.txt");
        var oversized = new string('x', 100_000) + "\n";
        await File.WriteAllTextAsync(path, oversized + oversized + ": You have entered Beach.\n");
        var timer = new ManualTailTimer();
        var identity = new FakeIdentity();
        var time = new ManualTime();
        var tail = new PollingFileTail(timer, 25, identityProvider: identity, timeProvider: time);
        var stream = new LogEventStream(tail, new LogParser());
        var diagnostics = new List<FileTailDiagnostic>();
        stream.Diagnostic += diagnostics.Add;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var events = stream.ReadAsync(path, 0, cts.Token).GetAsyncEnumerator();
        Assert.True(await events.MoveNextAsync());
        Assert.IsType<AreaEntered>(events.Current);
        Assert.Single(diagnostics);
        Assert.Equal(FileTailCondition.OversizedLine, diagnostics[0].Condition);
        // More than 24 reads were necessary, all on the one handle opened this poll.
        Assert.Equal(1, identity.Reads);
        var next = events.MoveNextAsync().AsTask();
        var poll = await timer.NextAsync(cts.Token);
        identity.Failure = new UnauthorizedAccessException("private account path");
        poll.Release();
        poll = await timer.NextAsync(cts.Token);
        Assert.Equal(50, poll.Milliseconds);
        Assert.Equal(2, diagnostics.Count);
        Assert.Equal(FileTailCondition.AccessDenied, diagnostics[1].Condition);
        poll.Release();
        poll = await timer.NextAsync(cts.Token);
        Assert.Equal(100, poll.Milliseconds);
        Assert.Equal(2, diagnostics.Count);
        time.AdvanceMinute();
        poll.Release();
        poll = await timer.NextAsync(cts.Token);
        Assert.Equal(3, diagnostics.Count);
        identity.Failure = new IOException("private account path");
        poll.Release();
        poll = await timer.NextAsync(cts.Token);
        Assert.Equal(FileTailCondition.Unavailable, diagnostics[3].Condition);
        identity.Failure = null;
        await File.AppendAllTextAsync(path, oversized + ": You have entered Forest.\n");
        poll.Release();
        Assert.True(await next);
        Assert.Equal("Forest", Assert.IsType<AreaEntered>(events.Current).Name);
        Assert.Equal(5, diagnostics.Count);
        Assert.Equal(FileTailCondition.OversizedLine, diagnostics[4].Condition);
        Assert.All(diagnostics, d => Assert.DoesNotContain("private", d.Message));
        next = events.MoveNextAsync().AsTask();
        poll = await timer.NextAsync(cts.Token);
        Assert.Equal(25, poll.Milliseconds);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
    }

    [Fact]
    public async Task UnstableCreationTimeDoesNotReplayAppendsButDetectsChangedPrefix()
    {
        var path = Path.Combine(directory, "Client.txt");
        await File.WriteAllTextAsync(path, "old\n");
        var timer = new ManualTailTimer();
        var identity = new FakeIdentity();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lines = new PollingFileTail(timer, identityProvider: identity)
            .ReadLinesAsync(path, cancellationToken: cts.Token).GetAsyncEnumerator();
        var next = lines.MoveNextAsync().AsTask();
        var poll = await timer.NextAsync(cts.Token);
        await File.AppendAllTextAsync(path, "new\n");
        poll.Release();
        Assert.True(await next);
        Assert.Equal("new", lines.Current); // old was not replayed despite a different creation timestamp
        next = lines.MoveNextAsync().AsTask();
        poll = await timer.NextAsync(cts.Token);
        await File.WriteAllTextAsync(path, "replacement_longer\n");
        poll.Release();
        Assert.True(await next);
        Assert.Equal("replacement_longer", lines.Current);
    }

    private sealed class FakeIdentity : IFileTailIdentityProvider
    {
        internal int Reads { get; private set; }
        internal Exception? Failure { get; set; }
        public FileTailIdentity Read(FileStream file)
        {
            Reads++;
            if (Failure is not null) throw Failure;
            return new(DateTime.UnixEpoch.AddSeconds(Reads), false);
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        internal void AdvanceMinute() => timestamp += TimeSpan.TicksPerMinute;
    }
}
