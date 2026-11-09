using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class FirstUseCalibrationTests
{
    [Fact]
    public void ConfirmationInvalidatesPreviousPageOnceWithoutDiscardingPendingFit()
    {
        var policy = new StashWatchPolicy(); var guard = new FirstUseCalibration();
        var page = GridFixture.Layout; var size = page.ReferenceResolution;
        var headerA = new double[PageSignature.Length];
        var headerB = Enumerable.Repeat(.2, PageSignature.Length).ToArray();
        var now = DateTimeOffset.UnixEpoch; var fit = new Transform(1, 0, 0);
        policy.Accept(headerA, null);
        Assert.True(policy.Tick(now, headerB, null).Changed);
        Assert.False(guard.Observe(page, size, fit));
        Assert.True(policy.Unknown(now, true, headerB)); // Invalidate A once.
        Assert.Equal(fit, guard.Pending(page, size));
        Assert.Equal(WatchWork.None, policy.Tick(now.AddSeconds(1), headerB, null).Work);
        Assert.Equal(WatchWork.Precheck, policy.Tick(now.Add(policy.Interval), headerB, null).Work);
        var refined = fit with { X = 1.1 };
        Assert.False(guard.Observe(page, size, refined));
        Assert.False(policy.Unknown(now.Add(policy.Interval), true, headerB));
        Assert.Equal(refined, guard.Pending(page, size));
        Assert.True(guard.Observe(page, size, refined));
        policy.Accept(headerB, null);
        Assert.True(policy.Identified);
    }

    [Fact]
    public void RequiresConsecutivePageResolutionAndWholeGridAgreement()
    {
        var guard = new FirstUseCalibration(); var page = GridFixture.Layout;
        var size = page.ReferenceResolution; var t = new Transform(1, 0, 0);
        Assert.False(guard.Observe(page, size, t));
        Assert.Equal(t, guard.Pending(page, size));
        Assert.True(guard.Observe(page, size, t with { X = .9, Y = -.9 }));
        Assert.False(guard.Observe(page, size, t with { X = 2.1 }));
        Assert.True(guard.Observe(page, size, t with { X = 2.1 }));
        Assert.False(guard.Observe(page with { Page = "other" }, size, t));
        Assert.False(guard.Observe(page, size, t));
        Assert.False(guard.Observe(page, new(2000, 1200), t));
        Assert.Null(guard.Pending(page, size));
        guard.Reset();
        Assert.False(guard.Observe(page, size, t));
        Assert.False(guard.Observe(page, size, t with { Scale = 1.01 }));
        Assert.False(guard.Observe(null, size, null));
        Assert.False(guard.Observe(page, size, t));
        guard.Reset();
        Assert.False(guard.Observe(page, size, t));
        Assert.True(guard.Observe(page, size, t));
    }
}
