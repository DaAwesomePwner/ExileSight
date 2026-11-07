using Windows.Win32;
using Windows.Win32.Foundation;

namespace ExileSight.Platform.Windows;

// Compile-time proof that BOTH independently owned NativeMethods.txt files feed CsWin32.
internal static class NativeGenerationProof
{
    internal static unsafe bool TryGetForegroundRect(out RECT rect)
    {
        RECT value = default;
        var success = PInvoke.GetWindowRect(PInvoke.GetForegroundWindow(), &value);
        rect = value;
        return success;
    }
}
