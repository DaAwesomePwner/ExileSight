using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Window;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Window;

public sealed class ScreenSamplerTests
{
    internal static GameWindowState Ready => new(true, 123, new(-1920, 20, 1920, 1080),
        96, true, false, default);

    [Fact]
    public void RefusesUnavailableWindowStatesAndReadsCurrentOnEveryCall()
    {
        using var window = new SampleWindow();
        var sampler = new ScreenSampler(window);
        foreach (var state in new[] { GameWindowState.Missing, Ready with { Found = false },
            Ready with { Handle = 0 }, Ready with { IsForeground = false },
            Ready with { IsMinimized = true }, Ready with { ClientRect = default } })
        {
            window.Current = state;
            Assert.False(ScreenSampler.CanSample(state, new(0, 0, 1, 1)));
            Assert.Null(sampler.TrySample(new(0, 0, 1, 1)));
        }
        Assert.Equal(6, window.Reads);
    }

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(0, 0, -1, 1)]
    [InlineData(0, 0, 1, -1)]
    [InlineData(-1, 0, 1, 1)]
    [InlineData(0, -1, 1, 1)]
    [InlineData(1919, 0, 2, 1)]
    [InlineData(0, 1079, 1, 2)]
    [InlineData(0, 0, 1025, 1024)]
    [InlineData(int.MaxValue, 0, 1, 1)]
    [InlineData(0, int.MaxValue, 1, 1)]
    [InlineData(0, 0, int.MaxValue, int.MaxValue)]
    public void RefusesInvalidRegionsBeforeNativeAccess(int x, int y, int width, int height)
    {
        using var window = new SampleWindow { Current = Ready };
        Assert.Null(new ScreenSampler(window).TrySample(new(x, y, width, height)));
        Assert.False(ScreenSampler.CanSample(Ready, new(x, y, width, height)));
    }

    [Theory]
    [InlineData(0, 0, 1024, 1024)]
    [InlineData(1919, 1079, 1, 1)]
    public void AcceptsLimitAndClientEdgeOnNegativeMonitor(int x, int y, int width, int height) =>
        Assert.True(ScreenSampler.CanSample(Ready, new(x, y, width, height)));

    [Fact]
    public void RefusesScreenCoordinateOverflow() =>
        Assert.False(ScreenSampler.CanSample(Ready with { ClientRect = new(int.MaxValue, 0, 100, 100) },
            new(1, 0, 1, 1)));

    internal sealed class SampleWindow : IGameWindow
    {
        private GameWindowState state = GameWindowState.Missing;
        internal int Reads { get; private set; }
        public GameWindowState Current { get { Reads++; return state; } set => state = value; }
        public string? LastError => null;
        public event EventHandler? Changed { add { } remove { } }
        public void Start() { }
        public void Dispose() { }
    }
}
