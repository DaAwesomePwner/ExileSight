using System.Text;
using ExileSight.Core.Logs.Tail;
using Xunit;

namespace ExileSight.Core.Tests.Logs;

public sealed class TailRegressionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "exilesight-tail-" + Guid.NewGuid());
    public TailRegressionTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);

    [Fact]
    public async Task TruncateAndRegrowBeyondOffsetUsesCheckpoint()
    {
        var path = Path.Combine(directory, "Client.txt");
        // Keep both the creation time and first 256 bytes unchanged. Only the consumed suffix changes.
        var prefix = new string('a', 300) + "\n";
        await File.WriteAllTextAsync(path, prefix + "old\n");
        var creation = File.GetCreationTimeUtc(path);
        var timer = new ManualTailTimer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lines = new PollingFileTail(timer).ReadLinesAsync(path, cancellationToken: cts.Token).GetAsyncEnumerator();
        var next = lines.MoveNextAsync().AsTask();
        var poll = await timer.NextAsync(cts.Token);
        await File.WriteAllTextAsync(path, prefix + "replacement_longer\n");
        Assert.Equal(creation, File.GetCreationTimeUtc(path));
        poll.Release();
        Assert.True(await next);
        Assert.Equal(prefix.TrimEnd('\n'), lines.Current);
        Assert.True(await lines.MoveNextAsync());
        Assert.Equal("replacement_longer", lines.Current);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(3L)]
    public async Task StartingMidLineDiscardsRemainder(long? position)
    {
        var path = Path.Combine(directory, "Client.txt");
        await File.WriteAllTextAsync(path, "partial");
        var timer = new ManualTailTimer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lines = new PollingFileTail(timer).ReadLinesAsync(path, position, cts.Token).GetAsyncEnumerator();
        var next = lines.MoveNextAsync().AsTask();
        var poll = await timer.NextAsync(cts.Token);
        await TailTests.Append(path, Encoding.UTF8.GetBytes("_remainder\ncomplete\n"));
        poll.Release();
        Assert.True(await next);
        Assert.Equal("complete", lines.Current);
    }

    [Fact]
    public async Task OversizeLineIsDiscardedOnceAndFollowingLineSurvives()
    {
        var path = Path.Combine(directory, "Client.txt");
        await File.WriteAllTextAsync(path, new string('x', 100_000));
        var warnings = new List<string>();
        var timer = new ManualTailTimer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lines = new PollingFileTail(timer, warning: warnings.Add).ReadLinesAsync(path, 0, cts.Token).GetAsyncEnumerator();
        var next = lines.MoveNextAsync().AsTask();
        var poll = await timer.NextAsync(cts.Token);
        Assert.False(next.IsCompleted);
        Assert.Single(warnings);
        await TailTests.Append(path, Encoding.UTF8.GetBytes("\ncomplete\n"));
        poll.Release();
        Assert.True(await next);
        Assert.Equal("complete", lines.Current);
        Assert.Single(warnings);
    }

    [Fact]
    public async Task MissingDuringRotationBacksOffThenRecovers()
    {
        var path = Path.Combine(directory, "Client.txt");
        await File.WriteAllTextAsync(path, "old\n");
        var timer = new ManualTailTimer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lines = new PollingFileTail(timer, 350).ReadLinesAsync(path, cancellationToken: cts.Token).GetAsyncEnumerator();
        var next = lines.MoveNextAsync().AsTask();
        var poll = await timer.NextAsync(cts.Token);
        File.Move(path, path + ".old");
        foreach (var expected in new[] { 700, 1400, 2800, 5000, 5000 })
        {
            poll.Release();
            poll = await timer.NextAsync(cts.Token);
            Assert.Equal(expected, poll.Milliseconds);
            Assert.False(next.IsCompleted);
        }
        await File.WriteAllTextAsync(path, "new\n");
        poll.Release();
        Assert.True(await next);
        Assert.Equal("new", lines.Current);
        next = lines.MoveNextAsync().AsTask();
        poll = await timer.NextAsync(cts.Token);
        Assert.Equal(350, poll.Milliseconds);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
    }

    [Fact]
    public async Task SharingViolationRetriesAndRecovers()
    {
        var path = Path.Combine(directory, "Client.txt");
        await File.WriteAllTextAsync(path, "ready\n");
        var timer = new ManualTailTimer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lines = new PollingFileTail(timer, 25).ReadLinesAsync(path, 0, cts.Token).GetAsyncEnumerator();
        Task<bool> next;
        ManualTailTimer.Poll poll;
        using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            next = lines.MoveNextAsync().AsTask();
            poll = await timer.NextAsync(cts.Token);
            Assert.Equal(50, poll.Milliseconds);
            Assert.False(next.IsCompleted);
        }
        poll.Release();
        Assert.True(await next);
        Assert.Equal("ready", lines.Current);
    }

    [Fact]
    public async Task DefaultAsyncTimerCancelsWhileIdle()
    {
        var path = Path.Combine(directory, "Client.txt");
        await File.WriteAllTextAsync(path, "");
        using var cts = new CancellationTokenSource();
        await using var lines = new PollingFileTail(pollingMilliseconds: 60_000).ReadLinesAsync(path, cancellationToken: cts.Token).GetAsyncEnumerator();
        var next = lines.MoveNextAsync().AsTask();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next.WaitAsync(TimeSpan.FromSeconds(30)));
    }
}
