using ExileSight.Core.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class OverlayClickPairingTests
{
    private static OverlayClickPairing Pair()
    {
        var pair = new OverlayClickPairing();
        pair.SetTargets([new(123, 0, 0, 100, 100)]);
        return pair;
    }
    [Fact]
    public void OverlapUsesZOrderInsteadOfRegistrationOrder()
    {
        var pair = new OverlayClickPairing();
        pair.SetTargets([new(123, 0, 0, 100, 100, 5), new(456, 0, 0, 100, 100, 1)]);
        pair.Observe(true, false, 20, 20, 100); pair.Observe(false, false, 20, 20, 200);
        Assert.NotNull(pair.Redeem(456, 20, 20, 200));
        pair.SetTargets([new(123, 0, 0, 100, 100, 0), new(456, 0, 0, 100, 100, 1)]);
        pair.Observe(true, false, 20, 20, 300); pair.Observe(false, false, 20, 20, 400);
        Assert.NotNull(pair.Redeem(123, 20, 20, 400));
    }
    [Fact]
    public void PhysicalClickMatchesOnlyOnce()
    {
        var pair = Pair(); pair.Observe(true, false, 20, 20, 100); pair.Observe(false, false, 21, 21, 200);
        Assert.NotNull(pair.Redeem(123, 22, 22, 700));
        Assert.Null(pair.Redeem(123, 22, 22, 700));
    }
    [Theory]
    [InlineData(true, false, 123, 20, 200)]
    [InlineData(false, true, 123, 20, 200)]
    [InlineData(false, false, 124, 20, 200)]
    [InlineData(false, false, 123, 30, 200)]
    [InlineData(false, false, 123, 20, 701)]
    [InlineData(false, false, 123, 20, 199)]
    public void RejectsInjectionMismatchAndExpiry(bool injectedDown, bool injectedUp, int window, int x, long now)
    {
        var pair = Pair(); pair.Observe(true, injectedDown, 20, 20, 100); pair.Observe(false, injectedUp, 20, 20, 200);
        Assert.Null(pair.Redeem(window, x, 20, now));
    }
    [Fact]
    public void NoDownOrRemovedWindowCannotRedeem()
    {
        var pair = Pair(); pair.Observe(false, false, 20, 20, 200);
        Assert.Null(pair.Redeem(123, 20, 20, 200));
        pair.Observe(true, false, 20, 20, 300); pair.Observe(false, false, 20, 20, 400);
        pair.SetTargets([]);
        Assert.Null(pair.Redeem(123, 20, 20, 400));
    }
}
