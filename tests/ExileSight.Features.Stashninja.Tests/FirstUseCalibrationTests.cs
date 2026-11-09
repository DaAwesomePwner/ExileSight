using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class FirstUseCalibrationTests
{
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
