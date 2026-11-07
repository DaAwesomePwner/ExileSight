using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.Features.Macros;

public static class WheelPlacement
{
    public static PixelRect Center(WheelOrigin origin, PixelRect client, double scale)
    {
        if (client.Width <= 0 || client.Height <= 0 || !double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(client));
        var width = (int)Math.Min(client.Width, 400 * scale);
        var height = (int)Math.Min(client.Height, 300 * scale);
        return new(Math.Clamp(origin.X - width / 2, client.X, client.X + client.Width - width),
            Math.Clamp(origin.Y - height / 2, client.Y, client.Y + client.Height - height), width, height);
    }
}
