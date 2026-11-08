using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Features.Stashninja;

internal static class GridCapture
{
    public static GridSample? Sample(IScreenSampler sampler, IGameWindow game, GameWindowState initial, GridArea area, Func<bool> allowed) =>
        GridSampling.Sample(area, tile =>
        {
            var sample = sampler.TrySample(new(tile.X, tile.Y, tile.Width, tile.Height));
            return sample is null ? null : new(tile.X, tile.Y, sample.Width, sample.Height, sample.Bgra);
        }, () => allowed() && Current(game.Current, initial));

    public static bool Current(GameWindowState state, GameWindowState initial) => state.Found && state.IsForeground &&
        !state.IsMinimized && state.Handle == initial.Handle && state.ClientRect == initial.ClientRect && state.Dpi == initial.Dpi;
}
