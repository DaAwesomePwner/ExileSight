using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Window;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit;
using Windows.Win32.Graphics.Gdi;

namespace ExileSight.Platform.Windows.Tests.Window;

[Collection("Desktop input")]
public sealed class ScreenSamplerIntegrationTests
{
    [WindowsOnlyFact]
    public unsafe void SamplesOwnedWindowAndRefusesStaleOrDestroyedWindow()
    {
        HWND hwnd;
        fixed (char* name = "STATIC")
            hwnd = PInvoke.CreateWindowEx(0, name, null,
                WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_VISIBLE, 40, 40, 100, 100,
                default, default, default, null);
        Assert.False(hwnd.IsNull);
        try
        {
            Assert.True(PInvoke.SetForegroundWindow(hwnd));
            using var window = new ScreenSamplerTests.SampleWindow
            {
                Current = new WindowSource().State((nint)hwnd.Value)
            };
            var state = window.Current;
            Assert.True(state.IsForeground);
            PaintPattern(hwnd);
            var sampler = new ScreenSampler(window);
            var sample = sampler.TrySample(new(2, 3, 8, 6));
            Assert.NotNull(sample);
            Assert.Equal(8, sample.Width);
            Assert.Equal(6, sample.Height);
            Assert.Equal(192, sample.Bgra.Length);
            for (int i = 3; i < sample.Bgra.Length; i += 4) Assert.Equal(255, sample.Bgra[i]);

            for (var y = 0; y < sample.Height; y++)
            for (var x = 0; x < sample.Width; x++)
            {
                var offset = (y * sample.Width + x) * 4;
                Assert.Equal(y < 3 ? (byte)0 : (byte)255, sample.Bgra[offset]);
                Assert.Equal(0, sample.Bgra[offset + 1]);
                Assert.Equal(y < 3 ? (byte)255 : (byte)0, sample.Bgra[offset + 2]);
            }
            HWND other;
            fixed (char* name = "STATIC")
                other = PInvoke.CreateWindowEx(0, name, null, WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_VISIBLE,
                    200, 200, 100, 100, default, default, default, null);
            Assert.False(other.IsNull);
            try
            {
                Assert.True(PInvoke.SetForegroundWindow(other));
                Assert.Null(sampler.TrySample(new(2, 3, 8, 6))); // Cached foreground state is deliberately stale.
            }
            finally { PInvoke.DestroyWindow(other); }
            Assert.True(PInvoke.SetForegroundWindow(hwnd));

            // A queued tracker update must not allow reads using obsolete screen coordinates.
            window.Current = state with { ClientRect = state.ClientRect with { X = state.ClientRect.X + 1 } };
            Assert.Null(sampler.TrySample(new(0, 0, 1, 1)));
            window.Current = state;
            Assert.True(PInvoke.DestroyWindow(hwnd));
            hwnd = default;
            Assert.Null(sampler.TrySample(new(0, 0, 1, 1)));
        }
        finally
        {
            if (!hwnd.IsNull) PInvoke.DestroyWindow(hwnd);
        }
    }
    private static unsafe void PaintPattern(HWND hwnd)
    {
        var dc = PInvoke.GetDC(hwnd);
        var memory = PInvoke.CreateCompatibleDC(dc);
        BITMAPINFO info = new() { bmiHeader = new() { biSize = (uint)sizeof(BITMAPINFOHEADER),
            biWidth = 100, biHeight = -100, biPlanes = 1, biBitCount = 32 } };
        void* pixels = null;
        var bitmap = PInvoke.CreateDIBSection(dc, &info, DIB_USAGE.DIB_RGB_COLORS, &pixels, default, 0);
        Assert.False(bitmap.IsNull);
        var old = PInvoke.SelectObject(memory, bitmap);
        try
        {
            for (var y = 0; y < 100; y++)
            for (var x = 0; x < 100; x++) ((uint*)pixels)[y * 100 + x] = y < 6 ? 0x00FF0000u : 0x000000FFu;
            Assert.True(PInvoke.BitBlt(dc, 0, 0, 100, 100, memory, 0, 0, ROP_CODE.SRCCOPY));
            Assert.True(PInvoke.GdiFlush());
        }
        finally
        {
            PInvoke.SelectObject(memory, old); PInvoke.DeleteObject(bitmap);
            PInvoke.DeleteDC(memory); PInvoke.ReleaseDC(hwnd, dc);
        }
    }

}
