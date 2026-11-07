using ExileSight.Core.Features;
using ExileSight.Core.Games;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class FeatureWorkQueueFailureTests
{
    [Fact]
    public async Task HostRoutesQueueFailuresToCompositionReporter()
    {
        var feature = new CapturingFeature();
        var host = new FeatureHost([feature], (_, _) => null, (_, _, _) => { });
        var errors = new List<string>();
        host.ReportWorkFailure = errors.Add;
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        feature.Context!.DispatchFeature(() => throw new InvalidOperationException("private item"));
        feature.Context.DispatchFeature(() => finished.SetResult());
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(["InvalidOperationException"], errors);
    }
    private sealed class CapturingFeature : IFeature
    {
        public FeatureManifest Manifest { get; } = new("test", [Game.PoE1], [], [], [], "test");
        public IFeatureContext? Context { get; private set; }
        public void Register(IFeatureContext context) => Context = context;
    }
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
