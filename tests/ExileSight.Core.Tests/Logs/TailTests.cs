using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using ExileSight.Core.Logs;
using ExileSight.Core.Logs.Tail;
using ExileSight.Platform.Abstractions.Files;
using ExileSight.Platform.Abstractions.Time;
using Xunit;

namespace ExileSight.Core.Tests.Logs;

public sealed class TailTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "exilesight-tail-" + Guid.NewGuid());
    public TailTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialUtf8AndReset(bool rotate)
    {
        var path = Path.Combine(directory, "Client.txt");
        await File.WriteAllTextAsync(path, "old\n");
        var clock = new SignalingClock();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lines = new PollingFileTail(clock, 25).ReadLinesAsync(path, cancellationToken: cts.Token).GetAsyncEnumerator();
        var next = lines.MoveNextAsync().AsTask();
        await clock.Polled.Reader.ReadAsync(cts.Token);
        var utf8 = Encoding.UTF8.GetBytes("雪\r\n");
        await Append(path, utf8[..1]);
        await clock.Polled.Reader.ReadAsync(cts.Token);
        Assert.False(next.IsCompleted);
        await Append(path, utf8[1..]);
        Assert.True(await next);
        Assert.Equal("雪", lines.Current);
        // Pending old-file fragments must not leak into the replacement.
        await Append(path, Encoding.UTF8.GetBytes("unfinished"));
        next = lines.MoveNextAsync().AsTask();
        await clock.Polled.Reader.ReadAsync(cts.Token);
        if (rotate) File.Move(path, path + ".old");
        await File.WriteAllTextAsync(path, "new\n");
        Assert.True(await next);
        Assert.Equal("new", lines.Current);
        next = lines.MoveNextAsync().AsTask();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
    }

    [Fact]
    public async Task PositionBomAndMissingFile()
    {
        var path = Path.Combine(directory, "Client.txt");
        var clock = new SignalingClock();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lines = new PollingFileTail(clock, 25).ReadLinesAsync(path, 0, cts.Token).GetAsyncEnumerator();
        var next = lines.MoveNextAsync().AsTask();
        await clock.Polled.Reader.ReadAsync(cts.Token);
        await File.WriteAllTextAsync(path, "first\nsecond\n", new UTF8Encoding(true));
        Assert.True(await next);
        Assert.Equal("first", lines.Current);
        Assert.True(await lines.MoveNextAsync());
        Assert.Equal("second", lines.Current);
        await using var resumed = new PollingFileTail(clock).ReadLinesAsync(path, 9, cts.Token).GetAsyncEnumerator();
        Assert.True(await resumed.MoveNextAsync());
        Assert.Equal("second", resumed.Current);
    }

    [Fact]
    public async Task StreamUpdatesStateWithFakeTail()
    {
        var stream = new LogEventStream(new FakeTail(), new LogParser());
        var events = new List<LogEvent>();
        await foreach (var e in stream.ReadAsync("unused")) events.Add(e);
        Assert.Equal(3, events.Count);
        Assert.Equal("Beach", stream.LastKnownArea);
        Assert.Equal("Ember", stream.LastKnownCharacter);
    }

    private static async Task Append(string path, byte[] bytes)
    {
        await using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        await file.WriteAsync(bytes);
    }

    private sealed class SignalingClock : IClock
    {
        public Channel<bool> Polled { get; } = Channel.CreateUnbounded<bool>();
        public long Milliseconds => Environment.TickCount64;
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public void Delay(int milliseconds) { Polled.Writer.TryWrite(true); Thread.Sleep(milliseconds); }
    }

    private sealed class FakeTail : IFileTail
    {
        public async IAsyncEnumerable<string> ReadLinesAsync(string path, long? position = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return ": You have entered Beach.";
            yield return ": Ember (Witch) is now level 3";
            yield return "unrecognized";
        }
    }
}
