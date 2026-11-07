using ExileSight.Core.Features;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class FeatureWorkQueueFailureTests
{
    [Fact]
    public async Task ReportsOnlyTypeAndContinuesAfterHandlerAndReporterFailures()
    {
        var errors = new List<string>();
        var queue = new FeatureWorkQueue { ReportFailure = type => { errors.Add(type); throw new IOException(); } };
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Post(() => throw new InvalidOperationException("private chat"));
        queue.Post(() => finished.SetResult());
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(["InvalidOperationException"], errors);
    }
}
