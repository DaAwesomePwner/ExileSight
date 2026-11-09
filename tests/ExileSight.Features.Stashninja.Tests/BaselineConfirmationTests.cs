using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class BaselineConfirmationTests
{
    private static double[] S(double value) => Enumerable.Repeat(value, PageSignature.Length).ToArray();
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    [Fact]
    public void StalePassThenTwoFreshFailuresHideWithinTwoTicksAfterCompletion()
    {
        var policy = new StashWatchPolicy(); policy.Accept(S(0), S(0));
        policy.ObserveIdentification(Start, S(.2), S(.2)); // Stale frame falsely accepted P.
        Assert.Equal(WatchWork.None, policy.Tick(Start, S(.2), S(.2)).Work);
        Assert.Equal(WatchWork.Identify, policy.Tick(Start.AddMilliseconds(450), S(.2), S(.2)).Work);
        Assert.False(policy.Retain(Start.AddMilliseconds(550), false, S(.2), S(.2)));
        Assert.True(policy.Identified);
        Assert.Equal(WatchWork.Identify, policy.Tick(Start.AddMilliseconds(900), S(.2), S(.2)).Work);
        Assert.True(policy.Retain(Start.AddMilliseconds(1000), false, S(.2), S(.2)));
        Assert.False(policy.Identified); // Two dispatcher ticks plus capture/scoring; no extra 450ms delay after completion.
    }

    [Fact]
    public void CleanConfirmationEndsReadsButNewSubTabNeedsAnotherConfirmation()
    {
        var policy = new StashWatchPolicy();
        policy.ObserveIdentification(Start, S(0), S(0));
        Assert.Equal(WatchWork.Identify, policy.Tick(Start.AddMilliseconds(450), S(0), S(0)).Work);
        policy.ObserveIdentification(Start.AddMilliseconds(550), S(0), S(0));
        Assert.Equal(WatchWork.None, policy.Tick(Start.AddSeconds(1), S(0), S(0)).Work);
        policy.ObserveIdentification(Start.AddSeconds(2), S(0), S(.2));
        Assert.Equal(WatchWork.Identify, policy.Tick(Start.AddSeconds(2.45), S(0), S(.2)).Work);
    }

    [Fact]
    public void SlowChecksUseCompletionTimesAndAnUnconfirmedBaselineCannotExpireIntoTrust()
    {
        var policy = new StashWatchPolicy(); policy.ObserveIdentification(Start, S(.2), S(.2));
        // First check starts at .45s, finishes at 3s. Next finishes at 6s: completions are only 3s apart.
        Assert.Equal(WatchWork.Identify, policy.Tick(Start.AddSeconds(.45), S(.2), S(.2)).Work);
        Assert.False(policy.Retain(Start.AddSeconds(3), false, S(.2), S(.2)));
        Assert.Equal(WatchWork.Identify, policy.Tick(Start.AddSeconds(3.45), S(.2), S(.2)).Work);
        Assert.True(policy.Retain(Start.AddSeconds(6), false, S(.2), S(.2)));
        policy.ObserveIdentification(Start.AddSeconds(7), S(.2), S(.2));
        Assert.False(policy.Retain(Start.AddSeconds(8), false, S(.2), S(.2)));
        Assert.Equal(WatchWork.Identify, policy.Tick(Start.AddSeconds(13), S(.2), S(.2)).Work);
        Assert.False(policy.Retain(Start.AddSeconds(14), false, S(.2), S(.2)));
        Assert.Equal(WatchWork.Identify, policy.Tick(Start.AddMilliseconds(14450), S(.2), S(.2)).Work);
    }
}
