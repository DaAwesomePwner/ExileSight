using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Features.Stashninja;

internal static class GridCapture
{
    public static PixelRect Bounds(Layout layout, Resolution client, Transform start)
    {
        var origins = layout.Cells.Select(c => (Cell: c, Point: Layouts.Origin(layout, c))).ToArray();
        var margin = client.Height * GridLocator.TranslationRange + 8;
        var left = origins.Min(c => c.Point.X * start.Scale * .95 + start.X) - margin;
        var top = origins.Min(c => c.Point.Y * start.Scale * .95 + start.Y) - margin;
        var right = origins.Max(c => (c.Point.X + c.Cell.W) * start.Scale * 1.05 + start.X) + margin;
        var bottom = origins.Max(c => (c.Point.Y + c.Cell.H) * start.Scale * 1.05 + start.Y) + margin;
        var x = (int)Math.Clamp(Math.Floor(left), 0, client.Width);
        var y = (int)Math.Clamp(Math.Floor(top), 0, client.Height);
        return new(x, y, (int)Math.Clamp(Math.Ceiling(right), x, client.Width) - x,
            (int)Math.Clamp(Math.Ceiling(bottom), y, client.Height) - y);
    }

    public static GridSample? Sample(IScreenSampler sampler, IGameWindow game, GameWindowState initial, PixelRect area, Func<bool> allowed)
    {
        if (area.Width <= 0 || area.Height <= 0 || (long)area.Width * area.Height > GridLocator.MaxPixels) return null;
        var pixels = new byte[area.Width * area.Height * 4];
        // 512-square tiles are comfortably below the sampler's per-call cap.
        for (var y = 0; y < area.Height; y += 512)
        for (var x = 0; x < area.Width; x += 512)
        {
            if (!allowed() || !Current(game.Current, initial)) return null;
            var width = Math.Min(512, area.Width - x); var height = Math.Min(512, area.Height - y);
            var tile = sampler.TrySample(new(area.X + x, area.Y + y, width, height));
            if (tile is null || tile.Width != width || tile.Height != height || tile.Bgra.Length != width * height * 4) return null;
            for (var row = 0; row < height; row++)
                Buffer.BlockCopy(tile.Bgra, row * width * 4, pixels, ((y + row) * area.Width + x) * 4, width * 4);
        }
        return allowed() && Current(game.Current, initial) ? new(area.X, area.Y, area.Width, area.Height, pixels) : null;
    }
    public static bool Current(GameWindowState state, GameWindowState initial) => state.Found && state.IsForeground &&
        !state.IsMinimized && state.Handle == initial.Handle && state.ClientRect == initial.ClientRect && state.Dpi == initial.Dpi;
}
