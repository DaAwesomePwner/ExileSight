using ExileSight.Core.Input;
using Windows.Win32;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ExileSight.Platform.Windows.Input;

/// <summary>Refresh only off the hook thread. The callback's provider reads one volatile value.</summary>
public sealed class KeyboardRepeatSettings
{
    // Unknown settings disable silence recovery rather than risk treating a repeat as a new press.
    private long silenceMilliseconds = long.MaxValue;
    public long SilenceMilliseconds => Volatile.Read(ref silenceMilliseconds);

    public unsafe void Refresh()
    {
        var settings = new FILTERKEYS { cbSize = (uint)sizeof(FILTERKEYS) };
        var success = PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETFILTERKEYS,
            settings.cbSize, &settings, 0);
        Volatile.Write(ref silenceMilliseconds, success
            ? Calculate((settings.dwFlags & PInvoke.FKF_FILTERKEYSON) != 0, settings.iWaitMSec, settings.iDelayMSec, settings.iRepeatMSec)
            : long.MaxValue);
    }

    /// <summary>FKF_FILTERKEYSON is bit 0. SlowKeys acceptance time is conservatively added
    /// to the longer initial/repeat gap, then a 200 ms margin. Use 64-bit sums to avoid overflow.
    /// https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-filterkeys</summary>
    public static long Calculate(bool enabled, uint waitMilliseconds, uint delayMilliseconds, uint repeatMilliseconds) =>
        enabled ? Math.Max(HotkeyDispatcher.BlockedReleaseSilenceMilliseconds,
            (long)waitMilliseconds + Math.Max(delayMilliseconds, repeatMilliseconds) + 200)
        : HotkeyDispatcher.BlockedReleaseSilenceMilliseconds;
}
