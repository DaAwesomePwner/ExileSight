using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.Core.Overlay;

/// <summary>Cursor and bounds are physical pixels; sizes and offsets are 1080p design units.</summary>
public static class PopupPlacement
{
    public static PixelRect Place(PixelRect client, PixelRect workArea, int cursorX, int cursorY,
        double width, double height, double scale, double offset = 0, bool centered = false)
    {
        if (!double.IsFinite(scale) || scale <= 0 || !double.IsFinite(width) || width <= 0 ||
            !double.IsFinite(height) || height <= 0 || !double.IsFinite(offset))
            throw new ArgumentOutOfRangeException(nameof(scale));
        var area = Available(client, workArea);
        int w = (int)Math.Clamp(Math.Ceiling(width * scale), 1, area.Width);
        int h = (int)Math.Clamp(Math.Ceiling(height * scale), 1, area.Height);
        double x = cursorX + (centered ? -w / 2d : Math.Round(offset * scale));
        double y = cursorY + (centered ? -h / 2d : Math.Round(offset * scale));
        return new((int)Math.Clamp(x, area.X, area.X + area.Width - w),
            (int)Math.Clamp(y, area.Y, area.Y + area.Height - h), w, h);
    }

    private static PixelRect Available(PixelRect client, PixelRect work)
    {
        if (work.Width <= 0 || work.Height <= 0) throw new ArgumentOutOfRangeException(nameof(work));
        int x = Math.Max(client.X, work.X), y = Math.Max(client.Y, work.Y);
        int right = Math.Min(client.X + client.Width, work.X + work.Width);
        int bottom = Math.Min(client.Y + client.Height, work.Y + work.Height);
        // A window can be moved entirely offscreen; keep its popup on the nearest monitor.
        return right > x && bottom > y ? new(x, y, right - x, bottom - y) : work;
    }
}
