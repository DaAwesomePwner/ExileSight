using System.Runtime.InteropServices;
using ExileSight.Platform.Abstractions.Window;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace ExileSight.Platform.Windows.Window;

/// <summary>Passive desktop capture; each call owns and releases all of its GDI resources.</summary>
public sealed class ScreenSampler(IGameWindow gameWindow) : IScreenSampler
{
    public ScreenSample? TrySample(PixelRect region)
    {
        var state = gameWindow.Current;
        if (!CanSample(state, region) || !OperatingSystem.IsWindows()) return null;
        return Capture(state, region);
    }

    internal static bool CanSample(GameWindowState state, PixelRect region)
    {
        var client = state.ClientRect;
        return state.Found && state.Handle != 0 && state.IsForeground && !state.IsMinimized
            && region.X >= 0 && region.Y >= 0 && region.Width > 0 && region.Height > 0
            && (long)region.X + region.Width <= client.Width
            && (long)region.Y + region.Height <= client.Height
            && (long)region.Width * region.Height <= IScreenSampler.MaxPixels
            && (long)client.X + region.X >= int.MinValue
            && (long)client.X + region.X + region.Width <= int.MaxValue
            && (long)client.Y + region.Y >= int.MinValue
            && (long)client.Y + region.Y + region.Height <= int.MaxValue;
    }

    private static unsafe bool StillCurrent(GameWindowState state)
    {
        HWND hwnd = new(state.Handle);
        RECT rect;
        System.Drawing.Point origin = default;
        return PInvoke.IsWindow(hwnd) && PInvoke.IsWindowVisible(hwnd)
            && !PInvoke.IsIconic(hwnd) && PInvoke.GetForegroundWindow() == hwnd
            && PInvoke.GetClientRect(hwnd, &rect) && PInvoke.ClientToScreen(hwnd, &origin)
            && state.ClientRect == new PixelRect(origin.X, origin.Y,
                rect.right - rect.left, rect.bottom - rect.top);
    }

    private static unsafe ScreenSample? Capture(GameWindowState state, PixelRect region)
    {
        if (!StillCurrent(state)) return null;
        var screen = PInvoke.GetDC(default);
        if (screen.IsNull) return null;
        HDC memory = default;
        HBITMAP bitmap = default;
        HGDIOBJ previous = default;
        try
        {
            memory = PInvoke.CreateCompatibleDC(screen);
            if (memory.IsNull) return null;
            BITMAPINFO info = new()
            {
                bmiHeader = new()
                {
                    biSize = (uint)sizeof(BITMAPINFOHEADER),
                    biWidth = region.Width,
                    biHeight = -region.Height,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0 // BI_RGB (uncompressed)
                }
            };
            void* bits = null;
            bitmap = PInvoke.CreateDIBSection(screen, &info, DIB_USAGE.DIB_RGB_COLORS, &bits, default, 0);
            if (bitmap.IsNull || bits == null) return null;
            previous = PInvoke.SelectObject(memory, bitmap);
            if (previous.IsNull || previous == new HGDIOBJ(-1)) return null;
            // The tracker is asynchronous: refuse stale geometry or focus before reading the desktop.
            if (!StillCurrent(state) || !PInvoke.BitBlt(memory, 0, 0, region.Width, region.Height,
                screen, state.ClientRect.X + region.X, state.ClientRect.Y + region.Y, ROP_CODE.SRCCOPY)
                || !PInvoke.GdiFlush() || !StillCurrent(state)) return null;
            byte[] bytes = new byte[region.Width * region.Height * 4];
            Marshal.Copy((nint)bits, bytes, 0, bytes.Length);
            // BI_RGB has an unused fourth byte; expose opaque alpha in the BGRA contract.
            for (int i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
            return new(region.Width, region.Height, bytes);
        }
        finally
        {
            if (!previous.IsNull && previous != new HGDIOBJ(-1)) PInvoke.SelectObject(memory, previous);
            if (!bitmap.IsNull) PInvoke.DeleteObject(bitmap);
            if (!memory.IsNull) PInvoke.DeleteDC(memory);
            PInvoke.ReleaseDC(default, screen);
        }
    }
}
