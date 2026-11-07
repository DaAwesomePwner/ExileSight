namespace ExileSight.Core.Overlay;

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

    public static double Scale(double clientHeight, double? scaleOverride) =>
        scaleOverride is double value && double.IsFinite(value) && value > 0 ? value : Scale(clientHeight);

    // Behaviour reference: Exile-UI modules/_functions.ahk, black-bar compensation.
    public static OverlayRect Place(OverlayRect client, OverlayRect design, double? scaleOverride = null)
    {
        var content = Content(client.X, client.Y, client.Width, client.Height);
        var scale = Scale(client.Height, scaleOverride);
        return new(content.X + design.X * scale, content.Y + design.Y * scale,
            design.Width * scale, design.Height * scale);
    }

    public static bool MatchesStreaming(uint pid, IReadOnlySet<uint> snapshot, string? title) =>
        MatchesProcess(pid, snapshot) && IsStreamingGameTitle(title);

    public static OverlayRect Content(double x, double y, double width, double height)
    {
        double contentWidth = Math.Min(width, height * 21d / 9);
        return new(x + (width - contentWidth) / 2, y, contentWidth, height);
    }

    // Strict foreground policy: even the ExileSight debug window hides the overlay.
    public static bool IsVisible(bool found, bool foreground, bool minimized) =>
        found && foreground && !minimized;

    public static bool IsVisible(bool found, bool foreground, bool minimized, bool enabled, bool requested) =>
        enabled && requested && IsVisible(found, foreground, minimized);

    // Detection behaviour reference: Exile-UI, Exile UI.ahk (client detection).
    public static bool Matches(string? windowClass, bool isGeForceNow, string? title) =>
        string.Equals(windowClass, "POEWindowClass", StringComparison.Ordinal) ||
        (isGeForceNow && IsStreamingGameTitle(title));

    public static bool IsStreamingGameTitle(string? title) =>
        title?.Contains("Path of Exile", StringComparison.OrdinalIgnoreCase) == true;

    public static bool MatchesProcess(uint windowPid, IReadOnlySet<uint> geForceNowPids) =>
        windowPid != 0 && geForceNowPids.Contains(windowPid);
}
