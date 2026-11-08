using ExileSight.Core.Logs.Tail;
using ExileSight.Platform.Abstractions.Files;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Perf.Tests;

[CollectionDefinition("Allocation probe", DisableParallelization = true)]
public sealed class AllocationProbeCollection;

[Collection("Allocation probe")]
public sealed class TailAllocationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuietLogPollingDoesNotAllocateAReadBufferEveryTick(bool populated)
    {
        var path = Path.GetTempFileName();
        try
        {
            if (populated) await File.WriteAllTextAsync(path, new string('x', 511) + "\n");
            // Warm up async/file infrastructure before counting the steady idle loop.
            await Poll(path, 10);
            var before = GC.GetTotalAllocatedBytes(precise: true);
            await Poll(path, 1000);
            var bytes = GC.GetTotalAllocatedBytes(precise: true) - before;
            output.WriteLine($"Quiet log (populated={populated}): {bytes} bytes / 1000 polls ({bytes / 1000d:F0} bytes/poll)");
            Assert.True(bytes < 2_000_000, $"Idle file tail allocated {bytes} bytes for 1000 quiet polls.");
        }
        finally { File.Delete(path); }
    }

    private static async Task Poll(string path, int count)
    {
        using var cancellation = new CancellationTokenSource();
        var tail = new PollingFileTail(new PollTimer(count, cancellation));
        await using var lines = tail.ReadLinesAsync(path, cancellationToken: cancellation.Token).GetAsyncEnumerator();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await lines.MoveNextAsync());
    }

    private sealed class PollTimer(int remaining, CancellationTokenSource cancellation) : IFileTailTimer
    {
        public Task DelayAsync(int milliseconds, CancellationToken token)
        {
            if (--remaining <= 0) cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
