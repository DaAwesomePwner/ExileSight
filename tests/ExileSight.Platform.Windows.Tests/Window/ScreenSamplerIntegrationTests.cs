using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Window;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit;

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
            var sampler = new ScreenSampler(window);
            var sample = sampler.TrySample(new(2, 3, 8, 6));
            Assert.NotNull(sample);
            Assert.Equal(8, sample.Width);
            Assert.Equal(6, sample.Height);
            Assert.Equal(192, sample.Bgra.Length);
            for (int i = 3; i < sample.Bgra.Length; i += 4) Assert.Equal(255, sample.Bgra[i]);

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
}
