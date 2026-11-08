using ExileSight.App.Features.Stashninja;
using ExileSight.Platform.Abstractions.Window;
using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class GridCaptureTests
{
    private sealed class Game : IGameWindow
    {
        public GameWindowState Current { get; set; } = new(true, 1, new(100, 200, 3000, 2000), 96, true, false, default);
        public string? LastError => null;
        public event EventHandler? Changed { add { } remove { } }
        public void Start() { }
        public void Dispose() { }
    }
    private sealed class Sampler(Action? after = null) : IScreenSampler
    {
        public List<PixelRect> Calls { get; } = [];
        public ScreenSample? TrySample(PixelRect rect)
        {
            Assert.InRange(rect.Width * rect.Height, 1, IScreenSampler.MaxPixels);
            Calls.Add(rect);
            var data = new byte[rect.Width * rect.Height * 4];
            for (var y = 0; y < rect.Height; y++)
            for (var x = 0; x < rect.Width; x++)
            { data[(y * rect.Width + x) * 4] = (byte)((rect.X + x) % 256); data[(y * rect.Width + x) * 4 + 1] = (byte)((rect.Y + y) % 256); }
            after?.Invoke();
            return new(rect.Width, rect.Height, data);
        }
    }
    [Fact]
    public void TilesLargeRegionsAndPreservesPixelCoordinates()
    {
        var game = new Game(); var sampler = new Sampler();
        var result = GridCapture.Sample(sampler, game, game.Current, new(45, 63, 1300, 1000), () => true);
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
        var game = new Game(); var allowed = true;
        var sampler = new Sampler(() => allowed = false);
        Assert.Null(GridCapture.Sample(sampler, game, game.Current, new(0, 0, 1300, 1000), () => allowed));
        Assert.Single(sampler.Calls);
        sampler = new Sampler(() => game.Current = game.Current with { IsForeground = false });
        Assert.Null(GridCapture.Sample(sampler, game, game.Current, new(0, 0, 1300, 1000), () => true));
        Assert.Single(sampler.Calls);
    }
    [Fact]
    public void OversizedCaptureDoesNotCallSampler()
    {
        var game = new Game(); var sampler = new Sampler();
        Assert.Null(GridCapture.Sample(sampler, game, game.Current, new(0, 0, 16384, 16384), () => true));
        Assert.Empty(sampler.Calls);
    }
}
