using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class HoverConfirmationTests
{
    private static double[] S(double value) => Enumerable.Repeat(value, PageSignature.Length).ToArray();
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EitherPassModeConfirmsTheSameSignatureExactlyOnce(bool firstNormal, bool secondNormal)
    {
        var policy = new StashWatchPolicy(); policy.Accept(S(0), S(0));
        Assert.False(policy.Retain(Now, true, S(.2), S(.2), adoptBaseline: firstNormal));
        Assert.Equal(WatchWork.None, policy.Tick(Now, S(.2), S(.2)).Work);
        Assert.Equal(WatchWork.Identify, policy.Tick(Now.AddMilliseconds(450), S(.2), S(.2)).Work);
        Assert.False(policy.Retain(Now.AddMilliseconds(550), true, S(.2), S(.2), adoptBaseline: secondNormal));
        for (var i = 2; i <= 100; i++)
            Assert.Equal(WatchWork.None, policy.Tick(Now.AddMilliseconds(i * 450), S(.2), S(.2)).Work);
        Assert.Equal(WatchWork.None, policy.Tick(Now.AddSeconds(46), S(0), S(0)).Work);
        Assert.Equal(WatchWork.Identify, policy.Tick(Now.AddMilliseconds(46450), S(0), S(0)).Work);
    }

    [Fact]
    public void HoverConfirmationFailureThenSecondFailureHidesAndPassResetsCount()
    {
        var policy = new StashWatchPolicy(); policy.Accept(S(0), S(0));
        policy.Retain(Now, true, S(.2), S(.2), adoptBaseline: false);
        Assert.Equal(WatchWork.Identify, policy.Tick(Now.AddMilliseconds(450), S(.2), S(.2)).Work);
        Assert.False(policy.Retain(Now.AddMilliseconds(550), false, S(.2), S(.2)));
        Assert.True(policy.Identified);
        Assert.Equal(WatchWork.Identify, policy.Tick(Now.AddMilliseconds(900), S(.2), S(.2)).Work);
        Assert.False(policy.Retain(Now.AddMilliseconds(1000), true, S(.2), S(.2), adoptBaseline: false));
        // Polish decision: after a failed confirmation, one passing capture is insufficient.
        Assert.Equal(WatchWork.Identify, policy.Tick(Now.AddMilliseconds(1350), S(.2), S(.2)).Work);
        Assert.False(policy.Retain(Now.AddMilliseconds(1450), false, S(.2), S(.2)));
        Assert.True(policy.Retain(Now.AddMilliseconds(1900), false, S(.2), S(.2)));
        Assert.False(policy.Identified);
    }

    [Theory]
    [InlineData(false, .4)]
    [InlineData(true, .4)]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    public void ChangedSignatureDuringConfirmationNeedsItsOwnFreshConfirmation(bool normal, double changed)
    {
        var policy = new StashWatchPolicy(); policy.Accept(S(0), S(0));
        policy.Retain(Now, true, S(.2), S(.2), adoptBaseline: false);
        Assert.Equal(WatchWork.Identify, policy.Tick(Now.AddMilliseconds(450), S(changed), S(changed)).Work);
        policy.Retain(Now.AddMilliseconds(550), true, S(changed), S(changed), adoptBaseline: normal);
        Assert.Equal(WatchWork.Identify, policy.Tick(Now.AddMilliseconds(900), S(changed), S(changed)).Work);
        policy.Retain(Now.AddMilliseconds(1000), true, S(changed), S(changed), adoptBaseline: normal);
        Assert.Equal(WatchWork.None, policy.Tick(Now.AddMilliseconds(1350), S(changed), S(changed)).Work);
    }
}
