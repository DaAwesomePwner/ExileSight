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

    /// <summary>Place an item/map card. The caller supplies the target monitor's work area.
    /// At exactly the client centre the card opens left (D-106). The 12 design-unit gap
    /// is horizontal only; the top aligns with the cursor. Dock fractions anchor the top-left
    /// in the full client before clamping. Legacy Place callers retain their existing behavior.</summary>
    public static PixelRect PlaceCard(PixelRect client, PixelRect workArea, int cursorX, int cursorY,
        double width, double height, double scale, CardPlacementOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.PlacementMode))
            throw new ArgumentOutOfRangeException(nameof(options));
        if (!double.IsFinite(options.DockedX) || options.DockedX < 0 || options.DockedX > 1 ||
            !double.IsFinite(options.DockedY) || options.DockedY < 0 || options.DockedY > 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        // Reuse the established dimension validation, rounding, size cap and offscreen fallback.
        var sized = Place(client, workArea, cursorX, cursorY, width, height, scale);
        var area = Available(client, workArea);
        double x, y;
        if (options.PlacementMode == CardPlacementMode.Docked)
        {
            x = client.X + Math.Round(client.Width * options.DockedX);
            y = client.Y + Math.Round(client.Height * options.DockedY);
        }
        else
        {
            var gap = Math.Round(12 * scale);
            x = cursorX > client.X + client.Width / 2d ? cursorX + gap : cursorX - sized.Width - gap;
            y = cursorY;
        }
        return new((int)Math.Clamp(x, area.X, area.X + area.Width - sized.Width),
            (int)Math.Clamp(y, area.Y, area.Y + area.Height - sized.Height), sized.Width, sized.Height);
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
