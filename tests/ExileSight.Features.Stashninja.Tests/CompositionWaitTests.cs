using System.Diagnostics;
using ExileSight.Platform.Abstractions.Window;
using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class CompositionWaitTests
{
    [Fact]
    public async Task NeverReturningSamplerTimesOutAndDoesNotAccumulateWorkers()
    {
        using var release = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        var sampler = new BlockedSampler(release, finished);
        var wait = new CompositionWait(sampler);
        try
        {
            for (var i = 0; i < 2; i++)
            {
                var elapsed = Stopwatch.StartNew();
                // Hang guard only: loaded CI runners can starve timer continuations for seconds.
                await wait.WaitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                Assert.True(elapsed.ElapsedMilliseconds >= CompositionWait.TimeoutMilliseconds + CompositionWait.FallbackMilliseconds - 5);
                Assert.False(finished.IsSet); // Caller resumed although the sampler never returned.
            }
            Assert.Equal(1, sampler.Calls);
        }
        finally
        {
            release.Set(); // Test teardown releases the otherwise non-returning fake.
            Assert.True(finished.Wait(TimeSpan.FromSeconds(2)));
        }
    }

    private sealed class BlockedSampler(ManualResetEventSlim release, ManualResetEventSlim finished) : IScreenSampler
    {
        public int Calls;
        public ScreenSample? TrySample(PixelRect region) => null;
        public void WaitForComposition()
        {
            Interlocked.Increment(ref Calls);
            release.Wait();
            finished.Set();
        }
    }
}
