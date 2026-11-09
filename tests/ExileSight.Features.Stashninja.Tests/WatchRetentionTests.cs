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
            Assert.Equal(WatchWork.None, policy.Tick(Now.AddMilliseconds(i * 450), S(i * .1), S(i * .1)).Work);
            Assert.True(policy.Identified);
        }
        Assert.Equal(WatchWork.Identify, policy.Tick(Now.AddMilliseconds(4950), S(1), S(1)).Work);
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
}
