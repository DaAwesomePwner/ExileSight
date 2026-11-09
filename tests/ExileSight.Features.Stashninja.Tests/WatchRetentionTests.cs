using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class WatchRetentionTests
{
    private static double[] S(double value) => Enumerable.Repeat(value, PageSignature.Length).ToArray();
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void HoverMotionWaitsForStabilityThenAdoptsKeptBaseline()
    {
        var policy = new StashWatchPolicy(); policy.Accept(S(0), S(0));
        for (var i = 1; i <= 10; i++)
        {
            Assert.Equal(i % 2 == 0 ? WatchWork.Identify : WatchWork.None, policy.Tick(Now.AddMilliseconds(i * 450), S(i * .1), S(i * .1)).Work);
            Assert.True(policy.Identified);
        }

        Assert.False(policy.Retain(Now, true, S(1), S(1)));
        for (var i = 0; i < 100; i++) Assert.Equal(WatchWork.None, policy.Tick(Now, S(1), S(1)).Work);
    }

    [Fact]
    public void UnknownWithGridSupportKeepsAndTwoIndependentFailedChecksHide()
    {
        var policy = new StashWatchPolicy(); policy.Accept(S(0), S(0));
        Assert.False(policy.Retain(Now, true, S(.1), S(.1))); // Tooltip or highlight disagreement.
        Assert.True(policy.Identified);
        Assert.Equal(WatchWork.None, policy.Tick(Now, S(.1), S(.1)).Work);
        Assert.False(policy.Retain(Now, false, S(.2), S(.2)));
        Assert.True(policy.Identified);
        Assert.True(policy.Retain(Now, false, S(.2), S(.2)));
        Assert.False(policy.Identified);
        Assert.False(policy.Retain(Now, false, S(.2), S(.2)));
    }

    [Fact]
    public void ConfidentIdentificationImmediatelyReplacesBaselineAndClearsFailure()
    {
        var policy = new StashWatchPolicy(); policy.Accept(S(0), S(0));
        Assert.False(policy.Retain(Now, false, S(.1), S(.1)));
        policy.Accept(S(.2), S(.2));
        Assert.True(policy.Identified);
        Assert.Equal(WatchWork.None, policy.Tick(Now, S(.2), S(.2)).Work);
        Assert.False(policy.Retain(Now, false, S(.2), S(.2)));
    }
    [Fact]
    public void MovingWorldClosesWithinFourTicksAndOldFailuresExpire()
    {
        var policy = new StashWatchPolicy(); policy.Accept(S(0), S(0));
        for (var i = 1; i <= 4; i++)
        {
            var now = Now.AddMilliseconds(i * 450);
            // Reviewer measured .058 mean signature distance for a 2px pan.
            if (policy.Tick(now, S(i * .058), S(i * .058)).Work == WatchWork.Identify)
                Assert.Equal(i == 4, policy.Retain(now, false, null, null));
        }
        Assert.False(policy.Identified);
        policy.Accept(S(0), S(0));
        Assert.False(policy.Retain(Now, false, null, null));
        policy.Tick(Now.AddSeconds(5), S(0), S(0));
        Assert.False(policy.Retain(Now.AddSeconds(5), false, null, null));
        Assert.True(policy.Identified);
    }

    [Fact]
    public void HoverPassResetsFailureWithoutAdoptingBaseline()
    {
        var policy = new StashWatchPolicy(); policy.Accept(S(0), S(0));
        Assert.False(policy.Retain(Now, false, null, null));
        Assert.False(policy.Retain(Now, true, S(.2), S(.2), adoptBaseline: false));
        Assert.Equal(WatchWork.None, policy.Tick(Now, S(.2), S(.2)).Work);
        // Round 2: a kept hover is acknowledged, rather than checked every tick.
        Assert.Equal(WatchWork.None, policy.Tick(Now.AddMilliseconds(450), S(.2), S(.2)).Work);
        Assert.Equal(WatchWork.None, policy.Tick(Now.AddMilliseconds(900), S(.4), S(.4)).Work);
        Assert.Equal(WatchWork.Identify, policy.Tick(Now.AddMilliseconds(1350), S(.4), S(.4)).Work);
        Assert.False(policy.Retain(Now.AddMilliseconds(450), false, null, null));
    }
}
