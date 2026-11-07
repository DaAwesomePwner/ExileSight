namespace ExileSight.Core.Overlay;

public readonly record struct OverlayRect(double X, double Y, double Width, double Height);

public static class OverlayRules
{
    // Physical screen coordinates divided by target DPI are useful for layout math,
    // but mixed-DPI desktop origins must be positioned natively in physical pixels.
    public static OverlayRect ToDips(double x, double y, double width, double height, uint dpi)
    {
        ArgumentOutOfRangeException.ThrowIfZero(dpi);
        double factor = 96d / dpi;
        return new(x * factor, y * factor, width * factor, height * factor);
    }

    public static double Scale(double clientHeight) => Math.Max(0, clientHeight) / 1080d;

    public static OverlayRect Content(double x, double y, double width, double height)
    {
        double contentWidth = Math.Min(width, height * 21d / 9);
        return new(x + (width - contentWidth) / 2, y, contentWidth, height);
    }

    // Strict foreground policy: even the ExileSight debug window hides the overlay.
    public static bool IsVisible(bool found, bool foreground, bool minimized) =>
        found && foreground && !minimized;

    // Detection behaviour reference: Exile-UI, Exile UI.ahk (client detection).
    public static bool Matches(string? windowClass, string? processName) =>
        string.Equals(windowClass, "POEWindowClass", StringComparison.Ordinal) ||
        string.Equals(processName, "GeForceNOW", StringComparison.OrdinalIgnoreCase);
}
