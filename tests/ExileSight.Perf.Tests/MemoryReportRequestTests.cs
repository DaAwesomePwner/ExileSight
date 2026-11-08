using ExileSight.App.Diagnostics;
using Xunit;

namespace ExileSight.Perf.Tests;

public sealed class MemoryReportRequestTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportWaitsForItsOwnRequestWithoutPolling(bool requestAlreadyExists)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var request = Path.Combine(directory, MemoryReportRequest.FileName);
            if (requestAlreadyExists) await File.WriteAllTextAsync(request, "snapshot");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var ready = MemoryReportRequest.WaitAsync(directory, deadline.Token);
            if (!requestAlreadyExists)
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "unrelated.txt"), "ignored");
                await Task.Delay(50);
                Assert.False(ready.IsCompleted);
                await File.WriteAllTextAsync(request, "snapshot");
            }
            await ready;
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task ShutdownCancelsTheWaitWithoutRequestingAReport()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var cancellation = new CancellationTokenSource();
            var ready = MemoryReportRequest.WaitAsync(directory, cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
