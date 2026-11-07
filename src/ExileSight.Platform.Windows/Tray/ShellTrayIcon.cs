using System.ComponentModel;
using ExileSight.Platform.Abstractions.Tray;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace ExileSight.Platform.Windows.Tray;

public sealed class ShellTrayIconFactory : ITrayIconFactory
{
    public ITrayIcon Create(nint callbackWindow, nint broadcastWindow) => new ShellTrayIcon(callbackWindow, broadcastWindow);
}

/// <summary>Uses only owned HWNDs and shared system icons; never opens a game process.</summary>
public sealed class ShellTrayIcon : ITrayIcon
{
    private const uint CallbackMessage = 0x8001, IconId = 1;
    private readonly nint callbackWindow, broadcastWindow;
    private readonly int ownerThread = Environment.CurrentManagedThreadId;
    private readonly uint taskbarCreated;
    private string tooltip = "ExileSight";
    private TrayIconImage icon;
    private bool disposed;
    public bool IsVisible { get; private set; }
    public event EventHandler? LeftClick;
    public event EventHandler? RightClick;
    public event EventHandler? DoubleClick;

    public unsafe ShellTrayIcon(nint callbackWindow, nint broadcastWindow)
    {
        RequireOwned(callbackWindow);
        RequireOwned(broadcastWindow);
        this.callbackWindow = callbackWindow;
        this.broadcastWindow = broadcastWindow;
        fixed (char* name = "TaskbarCreated") taskbarCreated = PInvoke.RegisterWindowMessage(name);
        if (taskbarCreated == 0) throw new Win32Exception();
    }

    public string Tooltip
    {
        get => tooltip;
        set
        {
            VerifyAccess();
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > 127) throw new ArgumentOutOfRangeException(nameof(value));
            tooltip = value;
            if (IsVisible) Update(NOTIFY_ICON_MESSAGE.NIM_MODIFY);
        }
    }
    public TrayIconImage Icon
    {
        get => icon;
        set
        {
            VerifyAccess();
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            icon = value;
            if (IsVisible) Update(NOTIFY_ICON_MESSAGE.NIM_MODIFY);
        }
    }

    public void Show()
    {
        VerifyAccess();
        if (IsVisible) return;
        Add();
        IsVisible = true;
    }

    private void Add()
    {
        Update(NOTIFY_ICON_MESSAGE.NIM_ADD);
        try { Update(NOTIFY_ICON_MESSAGE.NIM_SETVERSION); }
        catch { Delete(); throw; }
    }

    public void Hide()
    {
        VerifyAccess();
        if (!IsVisible) return;
        Delete();
        IsVisible = false;
    }

    public bool ProcessMessage(nint window, uint message, nint wParam, nint lParam)
    {
        VerifyAccess();
        if (window == broadcastWindow && message == taskbarCreated)
        {
            if (IsVisible) Add();
            return true;
        }
        if (!IsVisible || window != callbackWindow || message != CallbackMessage ||
            ((ulong)lParam >> 16 & 0xffff) != IconId) return false;
        switch ((uint)lParam & 0xffff)
        {
            case 0x400: // NIN_SELECT
            case 0x401: // NIN_KEYSELECT
                LeftClick?.Invoke(this, EventArgs.Empty); break;
            case 0x7b: // WM_CONTEXTMENU (mouse or keyboard)
                RightClick?.Invoke(this, EventArgs.Empty); break;
            case 0x203: // WM_LBUTTONDBLCLK
                DoubleClick?.Invoke(this, EventArgs.Empty); break;
            default: return false;
        }
        return true;
    }

    public void ActivateMenuOwner(nint menuWindow)
    {
        VerifyAccess();
        RequireOwned(menuWindow);
        PInvoke.SetForegroundWindow(new HWND(menuWindow));
    }

    private unsafe NOTIFYICONDATAW Data()
    {
        RequireOwned(callbackWindow);
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW), hWnd = new HWND(callbackWindow), uID = IconId,
            uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON |
                     NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP,
            uCallbackMessage = CallbackMessage
        };
        var resource = icon switch { TrayIconImage.Information => 32516, TrayIconImage.Warning => 32515, _ => 32512 };
        data.hIcon = PInvoke.LoadIcon(default, (char*)resource); // Shared system icon: do not destroy.
        if (data.hIcon.IsNull) throw new Win32Exception();
        tooltip.AsSpan().CopyTo(data.szTip.AsSpan());
        data.Anonymous.uVersion = 4; // NOTIFYICON_VERSION_4
        return data;
    }

    private unsafe void Update(NOTIFY_ICON_MESSAGE operation)
    {
        var data = Data();
        if (!PInvoke.Shell_NotifyIcon(operation, &data)) throw new Win32Exception("Could not update the ExileSight tray icon.");
    }

    private unsafe void Delete()
    {
        // Explorer may already have removed the icon; deletion is intentionally idempotent.
        var data = new NOTIFYICONDATAW { cbSize = (uint)sizeof(NOTIFYICONDATAW), hWnd = new HWND(callbackWindow), uID = IconId };
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, &data);
    }

    private static unsafe void RequireOwned(nint window)
    {
        uint process = 0;
        var thread = PInvoke.GetWindowThreadProcessId(new HWND(window), &process);
        if (window == 0 || process != Environment.ProcessId || thread != PInvoke.GetCurrentThreadId())
            throw new ArgumentException("Tray windows must belong to this process and the calling thread.", nameof(window));
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Environment.CurrentManagedThreadId != ownerThread)
            throw new InvalidOperationException("Tray operations must run on the owning UI thread.");
    }

    public void Dispose()
    {
        if (disposed) return;
        VerifyAccess();
        Hide();
        disposed = true;
    }
}
