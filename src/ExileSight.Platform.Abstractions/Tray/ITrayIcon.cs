namespace ExileSight.Platform.Abstractions.Tray;

public enum TrayIconImage { Application, Information, Warning }

/// <summary>UI-thread owned. Native messages come only from the application's own windows.</summary>
public interface ITrayIcon : IDisposable
{
    string Tooltip { get; set; }
    TrayIconImage Icon { get; set; }
    bool IsVisible { get; }
    event EventHandler? LeftClick;
    event EventHandler? RightClick;
    event EventHandler? DoubleClick;
    void Show();
    void Hide();
    bool ProcessMessage(nint window, uint message, nint wParam, nint lParam);
    void ActivateMenuOwner();
}

public interface ITrayIconFactory
{
    /// <summary>The callback window is message-only; the hidden top-level window receives Explorer broadcasts.</summary>
    ITrayIcon Create(nint callbackWindow, nint broadcastWindow);
}
