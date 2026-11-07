using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Core.Tests.Features;

public sealed class OmniDispatchTests
{
    [Fact]
    public void ContextOnlyExposesOwnedHandlersButHostRoutesAllInPriorityOrder()
    {
        IFeatureContext? first = null, second = null;
        var calls = new List<string>();
        var own = new Handler(() => calls.Add("one"), 1);
        var other = new Handler(() => calls.Add("two"), 10);
        var scheduled = new Queue<Action>();
        _ = new FeatureHost([
            new Module("one", c => { first = c; c.RegisterOmniHandler(own); }),
            new Module("two", c => { second = c; c.RegisterOmniHandler(other); })
        ], (_, _) => null, (_, _, _) => { }, dispatch: scheduled.Enqueue);
        Assert.Same(own, Assert.Single(first!.OmniHandlers));
        Assert.Same(other, Assert.Single(second!.OmniHandlers));
        first.DispatchOmni(new(), new(Game.PoE1), OmniPressKind.Short);
        Assert.Empty(calls);
        scheduled.Dequeue()();
        Assert.Equal(["two"], calls);
    }
    [Fact]
    public async Task SlowHandlerDoesNotBlockDispatchAndFailureDoesNotStopQueue()
    {
        using var entered = new ManualResetEventSlim();
        using var unblock = new ManualResetEventSlim();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new FeatureWorkQueue();
        queue.Post(() => { entered.Set(); unblock.Wait(); throw new InvalidOperationException(); });
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            queue.Post(() => finished.SetResult()); // returns despite the running handler
            Assert.False(finished.Task.IsCompleted);
        }
        finally { unblock.Set(); }
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    private sealed class Handler(Action handle, int priority) : IOmniHandler
    {
        public int Priority => priority;
        public bool Matches(ParsedItem item, OmniContext context) => true;
        public void Handle(ParsedItem item, OmniContext context, OmniPressKind press) => handle();
    }
    private sealed class Module(string id, Action<IFeatureContext> register) : IFeature
    {
        public FeatureManifest Manifest { get; } = new(id, [Game.PoE1], [], [], [], "test");
        public void Register(IFeatureContext context) => register(context);
    }
}
