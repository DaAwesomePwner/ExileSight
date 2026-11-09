using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class ConfirmationRecoveryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FailureRequiresTwoConsecutiveIndependentPasses(bool firstNormal, bool recoveryNormal)
    {
        var p = new StashWatchPolicy();
        var s = Enumerable.Repeat(.2, PageSignature.Length).ToArray();
        var start = DateTimeOffset.UnixEpoch;
        p.Accept(new double[PageSignature.Length], null);
        void Pass(int tick, bool normal) => p.Retain(start.AddMilliseconds(tick * 450), true, s, s, normal);
        void Fail(int tick) => Assert.False(p.Retain(start.AddMilliseconds(tick * 450), false, s, s));
        void Pending(int tick) => Assert.Equal(WatchWork.Identify,
            p.Tick(start.AddMilliseconds(tick * 450 + 1), s, s).Work);
        Pass(0, firstNormal); Fail(1); Pass(2, recoveryNormal); Pending(2); // stale, fresh failure, stale
        Fail(3); Pass(4, !recoveryNormal); Pending(4); // Failure resets successful-confirmation streak.
        Pass(5, recoveryNormal);
        Assert.Equal(WatchWork.None, p.Tick(start.AddSeconds(3), s, s).Work);
    }
}
