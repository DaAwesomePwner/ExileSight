using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ExileSight.Platform.Windows.Input;

/// <summary>Transaction-thread message-only window. No messages are sent to foreign windows.</summary>
internal sealed class ClipboardOwner : IDisposable
{
    private readonly WNDPROC callback;
    private readonly string className = "ExileSightClipboard" + Guid.NewGuid().ToString("N");
    internal HWND Handle { get; }
    internal byte[]? Payload { get; set; }
    internal bool Consumed { get; private set; }
    internal unsafe ClipboardOwner()
    {
        callback = WindowProc;
        fixed (char* name = className)
        {
            var cls = new WNDCLASSW { lpfnWndProc = callback, lpszClassName = name, hInstance = PInvoke.GetModuleHandle(null) };
            if (PInvoke.RegisterClass(in cls) == 0) throw new InvalidOperationException("Clipboard class registration failed");
            Handle = PInvoke.CreateWindowEx(0, name, null, 0, 0, 0, 0, 0, new HWND(-3), default, cls.hInstance, null);
            if (Handle.IsNull)
            {
                PInvoke.UnregisterClass(name, cls.hInstance);
                throw new InvalidOperationException("Clipboard window creation failed");
            }
        }
    }
    private bool rendered;
    internal Exception? RenderError { get; private set; }
    private LRESULT WindowProc(HWND window, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == 0x305 && wParam.Value == 13 && Payload is { } bytes) // WM_RENDERFORMAT
        {
            try
            {
                // Rendering proves a reader requested text; only attribute it to the game
                // when the clipboard opener is the foreground game window.
                var reader = PInvoke.GetOpenClipboardWindow();
                ClipboardMemory.Write(13, bytes); // WM_RENDERFORMAT must NOT OpenClipboard.
                rendered = true;
                Consumed = IsGameReader(reader);
            }
            catch (Exception error) { RenderError = error; }
            return default;
        }
        if (message == 0x306 && !rendered && Payload is { } pending) // WM_RENDERALLFORMATS
        {
            try
            {
                ClipboardMemory.Open(Handle);
                try
                {
                    if (PInvoke.GetClipboardOwner() == Handle)
                    { ClipboardMemory.Write(13, pending); rendered = true; }
                }
                finally { PInvoke.CloseClipboard(); }
            }
            catch (Exception error) { RenderError = error; }
            return default;
        }
        return PInvoke.DefWindowProc(window, message, wParam, lParam);
    }
    private static unsafe bool IsGameReader(HWND reader)
    {
        if (reader.IsNull) return false;
        var foreground = PInvoke.GetForegroundWindow();
        uint readerPid = 0, gamePid = 0;
        PInvoke.GetWindowThreadProcessId(reader, &readerPid);
        PInvoke.GetWindowThreadProcessId(foreground, &gamePid);
        return reader == foreground && readerPid != 0 && readerPid == gamePid && GameForeground.IsGame() &&
            foreground == PInvoke.GetForegroundWindow();
    }
    internal static unsafe void Pump()
    {
        MSG message;
        while (PInvoke.PeekMessage(&message, default, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
        {
            PInvoke.TranslateMessage(&message);
            PInvoke.DispatchMessage(&message);
        }
    }
    internal static unsafe void Wait(int milliseconds)
    {
        Pump();
        PInvoke.MsgWaitForMultipleObjectsEx(0, null, (uint)milliseconds, QUEUE_STATUS_FLAGS.QS_ALLINPUT,
            MSG_WAIT_FOR_MULTIPLE_OBJECTS_EX_FLAGS.MWMO_INPUTAVAILABLE);
        Pump();
    }
    public unsafe void Dispose()
    {
        PInvoke.DestroyWindow(Handle);
        fixed (char* name = className) PInvoke.UnregisterClass(name, PInvoke.GetModuleHandle(null));
        GC.KeepAlive(callback);
    }
}
