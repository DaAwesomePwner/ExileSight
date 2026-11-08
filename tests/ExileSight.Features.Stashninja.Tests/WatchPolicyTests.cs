using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class WatchPolicyTests
{
    private static double[] Signature(double value = 0) => Enumerable.Repeat(value, PageSignature.Length).ToArray();
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    [Fact]
    public void StableAndNoisyPagesNeverCaptureOrHide()
    {
        var policy = new StashWatchPolicy();
        policy.Accept(Signature(), Signature());
        for (var i = 0; i < 1000; i++)
        {
            var step = policy.Tick(Start.AddMilliseconds(i * 450), Signature(.01), Signature(-.01));
            Assert.Equal(WatchWork.None, step.Work);
            Assert.True(policy.Identified);
        }
    }

    [Theory]
    [InlineData(.06, 0)]
    [InlineData(0, .06)]
    public void EitherBaselineChangeTriggersIdentification(double header, double sub)
    {
        var policy = new StashWatchPolicy();
        policy.Accept(Signature(), Signature());
        Assert.Equal(WatchWork.None, policy.Tick(Start, Signature(.03), Signature(.03)).Work);
        Assert.Equal(WatchWork.Identify, policy.Tick(Start.AddMilliseconds(450), Signature(header), Signature(sub)).Work);
    }

    [Fact]
    public void UnknownDebouncesAndInvalidatesOnlyOnce()
    {
        var policy = new StashWatchPolicy();
        policy.Accept(Signature(), null);
        Assert.False(policy.Unknown(Start, false, null));
        Assert.False(policy.Unknown(Start.AddMilliseconds(900), false, null));
        Assert.True(policy.Unknown(Start.AddMilliseconds(1350), false, null));
        Assert.False(policy.Unknown(Start.AddSeconds(2), false, null));
        policy.Accept(Signature(), null);
        Assert.True(policy.Unknown(Start, true, Signature(.1)));
    }

    [Fact]
    public void SuccessfulBaselineReadBreaksConsecutiveUnknown()
    {
        var policy = new StashWatchPolicy();
        policy.Accept(Signature(), Signature());
        Assert.Equal(WatchWork.Unknown, policy.Tick(Start, Signature(), null).Work);
        Assert.False(policy.Unknown(Start, false, Signature()));
        Assert.Equal(WatchWork.None, policy.Tick(Start.AddMilliseconds(900), Signature(), Signature()).Work);
        Assert.False(policy.Unknown(Start.AddMilliseconds(1350), false, Signature()));
        Assert.False(policy.Unknown(Start.AddMilliseconds(1800), false, Signature()));
        Assert.True(policy.Unknown(Start.AddMilliseconds(2700), false, Signature()));
    }

    [Fact]
    public void NoStashBacksOffAndHeaderChangeResetsDelay()
    {
        var policy = new StashWatchPolicy();
        Assert.Equal(WatchWork.Precheck, policy.Tick(Start, Signature(), null).Work);
        for (var i = 0; i < 4; i++) policy.Unknown(Start, false, Signature());
        Assert.Equal(TimeSpan.FromSeconds(1.8), policy.Interval);
        Assert.Equal(WatchWork.None, policy.Tick(Start.AddSeconds(1), Signature(), null).Work);
        Assert.Equal(WatchWork.Precheck, policy.Tick(Start.AddSeconds(1), Signature(.1), null).Work);
        Assert.Equal(TimeSpan.FromMilliseconds(450), policy.Interval);
        Assert.False(StashWatchPolicy.ShouldIdentify(12.49));
        Assert.True(StashWatchPolicy.ShouldIdentify(12.5));
    }

    [Fact]
    public void PinSurvivesSubTabChangeButEndsOnHeaderChangeOrClosedStash()
    {
        var policy = new StashWatchPolicy();
        policy.Accept(Signature(), Signature(), pinned: true);
        Assert.Equal(WatchWork.None, policy.Tick(Start, Signature(), Signature(.2)).Work);
        Assert.True(policy.Pinned);
        Assert.Equal(WatchWork.Identify, policy.Tick(Start, Signature(.2), Signature()).Work);
        Assert.False(policy.Pinned);
        policy.Accept(Signature(), null, pinned: true);
        Assert.True(policy.Unknown(Start, true, null));
        Assert.False(policy.Pinned);
    }
}
