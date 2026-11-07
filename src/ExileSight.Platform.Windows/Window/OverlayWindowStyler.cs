using System.ComponentModel;
using ExileSight.Platform.Abstractions.Window;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ExileSight.Platform.Windows.Window;

public sealed class OverlayWindowStyler : IOverlayWindowStyler
{
    private const int Transparent = 0x20;
    private const int Required = 0x80000 | 0x08000000 | 0x80;

    public void Apply(nint handle) => SetStyles(handle, true);

    public void SetClickThrough(nint handle, bool enabled) => SetStyles(handle, enabled);

    private static void SetStyles(nint handle, bool clickThrough)
    {
        HWND hwnd = Owned(handle);
        int styles = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) | Required;
        styles = clickThrough ? styles | Transparent : styles & ~Transparent;
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, styles);
        if (PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) != styles)
            throw new Win32Exception("Could not apply overlay styles.");
    }

    public unsafe PixelRect GetBounds(nint handle)
    {
        HWND hwnd = Owned(handle);
        RECT rect;
        if (!PInvoke.GetWindowRect(hwnd, &rect)) throw new Win32Exception();
        return new(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
    }

    public unsafe PixelRect GetWorkArea(PixelRect reference)
    {
        RECT rect = new() { left = reference.X, top = reference.Y,
            right = reference.X + reference.Width, bottom = reference.Y + reference.Height };
        var monitor = PInvoke.MonitorFromRect(&rect, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        MONITORINFO info = new() { cbSize = (uint)sizeof(MONITORINFO) };
        if (!PInvoke.GetMonitorInfo(monitor, &info)) throw new Win32Exception();
        var work = info.rcWork;
        return new(work.left, work.top, work.right - work.left, work.bottom - work.top);
    }

    public void Position(nint handle, PixelRect rect)
    {
        HWND hwnd = Owned(handle);
        if (!PInvoke.SetWindowPos(hwnd, new HWND(-1), rect.X, rect.Y, rect.Width, rect.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER))
            throw new Win32Exception();
    }

    private static unsafe HWND Owned(nint handle)
    {
        HWND hwnd = new(handle);
        uint pid = 0;
        PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        if (handle == 0 || pid != Environment.ProcessId)
            throw new ArgumentException("Overlay handle must belong to this process.", nameof(handle));
        return hwnd;
    }
}
