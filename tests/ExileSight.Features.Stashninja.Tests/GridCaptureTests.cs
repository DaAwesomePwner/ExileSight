using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class GridCaptureTests
{
    private sealed class Sampler(Action? after = null)
    {
        public List<GridArea> Calls { get; } = [];
        public GridSample? TrySample(GridArea rect)
        {
            Assert.InRange(rect.Width * rect.Height, 1, 1_048_576);
            Calls.Add(rect);
            var data = new byte[rect.Width * rect.Height * 4];
            for (var y = 0; y < rect.Height; y++)
            for (var x = 0; x < rect.Width; x++)
            { data[(y * rect.Width + x) * 4] = (byte)((rect.X + x) % 256); data[(y * rect.Width + x) * 4 + 1] = (byte)((rect.Y + y) % 256); }
            after?.Invoke();
            return new(rect.X, rect.Y, rect.Width, rect.Height, data);
        }
    }
    [Fact]
    public void TilesLargeRegionsAndPreservesPixelCoordinates()
    {
        var sampler = new Sampler();
        var result = GridSampling.Sample(new(45, 63, 1300, 1000), sampler.TrySample, () => true);
        Assert.NotNull(result); Assert.Equal(6, sampler.Calls.Count);
        for (var y = 0; y < result.Height; y++)
        for (var x = 0; x < result.Width; x++)
        {
            Assert.Equal((byte)((x + 45) % 256), result.Bgra[(y * result.Width + x) * 4]);
            Assert.Equal((byte)((y + 63) % 256), result.Bgra[(y * result.Width + x) * 4 + 1]);
        }
    }
    [Fact]
    public void StopsCapturingWhenPageChangesOrForegroundIsLost()
    {
        var allowed = true;
        var sampler = new Sampler(() => allowed = false);
        Assert.Null(GridSampling.Sample(new(0, 0, 1300, 1000), sampler.TrySample, () => allowed));
        Assert.Single(sampler.Calls);
        var foreground = true;
        sampler = new Sampler(() => foreground = false);
        Assert.Null(GridSampling.Sample(new(0, 0, 1300, 1000), sampler.TrySample, () => foreground));
        Assert.Single(sampler.Calls);
    }
    [Fact]
    public void OversizedCaptureDoesNotCallSampler()
    {
        var sampler = new Sampler();
        Assert.Null(GridSampling.Sample(new(0, 0, 16384, 16384), sampler.TrySample, () => true));
        Assert.Empty(sampler.Calls);
    }
}
