using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using ExileSight.Platform.Abstractions.Tray;

namespace ExileSight.App.Tray;

/// <summary>Owns the WPF menu and both native message sinks on the application dispatcher.</summary>
public sealed class TrayHome : IDisposable
{
    private readonly ContextMenu menu = new() { Placement = PlacementMode.MousePoint, StaysOpen = false };
    private readonly HwndSource callbacks;
    private readonly HwndSource broadcasts;
    private ITrayIcon? icon;
    private bool disposed;
    private readonly TrayDiagnostics diagnostics;

    public TrayHome(ITrayIconFactory factory, TrayDiagnostics diagnostics, Action debug, Action setup, Action folder, Action exit)
    {
        this.diagnostics = diagnostics;
        Add("Debug panel", debug);
        Add("Run setup again", setup);
        Add("Open settings folder", folder);
        Add("Exit", exit);
        callbacks = new(new HwndSourceParameters("ExileSight tray callbacks")
        {
            ParentWindow = new nint(-3), // HWND_MESSAGE: never visible, never targets the game.
            WindowStyle = 0, Width = 0, Height = 0
        });
        try
        {
            // Message-only windows do not receive broadcasts. This hidden, top-level tool
            // window exists solely to receive TaskbarCreated when Explorer restarts.
            broadcasts = new(new HwndSourceParameters("ExileSight tray broadcasts")
            {
                WindowStyle = unchecked((int)0x80000000), // WS_POPUP, without WS_VISIBLE
                ExtendedWindowStyle = 0x80, // WS_EX_TOOLWINDOW
                Width = 0, Height = 0
            });
            try
            {
                callbacks.AddHook(Receive);
                broadcasts.AddHook(Receive);
                icon = factory.Create(callbacks.Handle, broadcasts.Handle);
                icon.Tooltip = "ExileSight";
                icon.Icon = TrayIconImage.Application;
                icon.LeftClick += (_, _) => Dispatch(debug);
                icon.DoubleClick += (_, _) => Dispatch(debug);
                icon.RightClick += (_, _) => Dispatch(() => menu.IsOpen = true);
                menu.Opened += MenuOpened;
                try { icon.Show(); }
                catch (Win32Exception error) { diagnostics.Record(error); }
            }
            catch { icon?.Dispose(); broadcasts.Dispose(); throw; }
        }
        catch { callbacks.Dispose(); throw; }
    }

    private void Add(string title, Action action)
    {
        var item = new MenuItem { Header = title };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private void Dispatch(Action action)
    {
        // Do not create modal WPF windows from inside the native callback stack.
        callbacks.Dispatcher.BeginInvoke(new Action(() => { if (!disposed) action(); }));
    }

    private void MenuOpened(object sender, RoutedEventArgs e)
    {
        if (PresentationSource.FromVisual(menu) is HwndSource popup)
            icon?.ActivateMenuOwner(popup.Handle);
        menu.Focus();
    }

    private nint Receive(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (disposed || icon is null) return 0;
        try { handled = icon.ProcessMessage(window, (uint)message, wParam, lParam); }
        catch (Exception error)
        {
            // Never unwind a managed exception through the native window procedure.
            diagnostics.Record(error);
        }
        return 0;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        menu.IsOpen = false;
        menu.Opened -= MenuOpened;
        icon?.Dispose(); // Remove the icon before destroying its callback HWND.
        callbacks.RemoveHook(Receive);
        broadcasts.RemoveHook(Receive);
        callbacks.Dispose();
        broadcasts.Dispose();
    }
}
