using System.Diagnostics;
using Windows.Win32;

namespace ExileSight.Platform.Windows.Input;

public static class GameForeground
{
    public static unsafe bool IsGame()
    {
        var hwnd = PInvoke.GetForegroundWindow();
        if (hwnd.IsNull) return false;
        char* name = stackalloc char[256];
        var length = PInvoke.GetClassName(hwnd, name, 256);
        if (new string(name, 0, Math.Max(0, length)) == "POEWindowClass") return true;
        uint pid;
        PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        try { using var process = Process.GetProcessById((int)pid); return process.ProcessName == "GeForceNOW"; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }
}
