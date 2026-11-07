using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using ExileSight.Core.Logs;
using ExileSight.Core.Logs.Tail;
using ExileSight.Platform.Abstractions.Files;
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
        var timer = new ManualTailTimer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lines = new PollingFileTail(timer, 25).ReadLinesAsync(path, cancellationToken: cts.Token).GetAsyncEnumerator();
        var next = lines.MoveNextAsync().AsTask();
        var poll = await timer.NextAsync(cts.Token);
        var utf8 = Encoding.UTF8.GetBytes("雪\r\n");
        await Append(path, utf8[..1]);
        poll.Release();
        poll = await timer.NextAsync(cts.Token); // now the first UTF-8 byte has actually been read
        Assert.False(next.IsCompleted);
        await Append(path, utf8[1..]);
        poll.Release();
        Assert.True(await next);
        Assert.Equal("雪", lines.Current);
        next = lines.MoveNextAsync().AsTask();
        poll = await timer.NextAsync(cts.Token);
        await Append(path, Encoding.UTF8.GetBytes("unfinished"));
        poll.Release();
        poll = await timer.NextAsync(cts.Token);
        if (rotate) File.Move(path, path + ".old");
        await File.WriteAllTextAsync(path, "new\n");
        poll.Release();
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
        var timer = new ManualTailTimer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lines = new PollingFileTail(timer, 25).ReadLinesAsync(path, 0, cts.Token).GetAsyncEnumerator();
        var next = lines.MoveNextAsync().AsTask();
        var poll = await timer.NextAsync(cts.Token);
        Assert.Equal(50, poll.Milliseconds);
        await File.WriteAllTextAsync(path, "first\nsecond\n", new UTF8Encoding(true));
        poll.Release();
        Assert.True(await next);
        Assert.Equal("first", lines.Current);
        Assert.True(await lines.MoveNextAsync());
        Assert.Equal("second", lines.Current);
        await using var resumed = new PollingFileTail().ReadLinesAsync(path, 9, cts.Token).GetAsyncEnumerator();
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

    internal static async Task Append(string path, byte[] bytes)
    {
        await using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        await file.WriteAsync(bytes);
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

internal sealed class ManualTailTimer : IFileTailTimer
{
    private readonly Channel<Poll> polls = Channel.CreateUnbounded<Poll>();
    public ValueTask<Poll> NextAsync(CancellationToken token) => polls.Reader.ReadAsync(token);
    public async Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        var poll = new Poll(milliseconds);
        await polls.Writer.WriteAsync(poll, cancellationToken);
        await poll.Completion.Task.WaitAsync(cancellationToken);
    }
    internal sealed class Poll(int milliseconds)
    {
        internal int Milliseconds => milliseconds;
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Release() => Completion.SetResult();
    }
}
