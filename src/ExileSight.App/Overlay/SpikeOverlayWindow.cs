using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ExileSight.Core.Overlay;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Overlay;

public sealed class SpikeOverlayWindow : System.Windows.Window
{
    private readonly IOverlayWindowStyler styler;
    private readonly TextBlock label;
    private PixelRect rect;
    private nint handle;
    private bool positioning;
    public event EventHandler? RefreshRequested;

    public SpikeOverlayWindow(IOverlayWindowStyler styler)
    {
        this.styler = styler;
        AllowsTransparency = true;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        Focusable = false;
        Background = Brushes.Transparent;
        Width = Height = 1;
        label = new TextBlock
        {
            Foreground = Brushes.White, Background = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top, Padding = new Thickness(6)
        };
        Content = new Border { BorderBrush = Brushes.Cyan, BorderThickness = new Thickness(1), Child = label };
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            styler.Apply(handle);
        };
        DpiChanged += (_, _) =>
        {
            // WPF applies its new rendering transform first, then reassert the physical bounds.
            Dispatcher.BeginInvoke(() =>
            {
                if (!IsVisible) return;
                RefreshRequested?.Invoke(this, EventArgs.Empty);
            });
        };
    }

    public void InitializeHandle() => new WindowInteropHelper(this).EnsureHandle();

    public void Update(GameWindowState state)
    {
        rect = state.ClientRect;
        if (!OverlayRules.IsVisible(state.Found, state.IsForeground, state.IsMinimized) ||
            rect.Width <= 0 || rect.Height <= 0)
        {
            Hide();
            return;
        }
        if (!IsVisible) Show();
        Position();
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        var content = OverlayRules.Content(0, 0, rect.Width, rect.Height);
        var dpi = VisualTreeHelper.GetDpi(this);
        var dips = OverlayRules.ToDips(content.X, content.Y, content.Width, content.Height,
            (uint)Math.Round(dpi.PixelsPerInchX));
        label.Margin = new Thickness(dips.X + 8, dips.Y + 8, 0, 0);
        label.Text = $"ExileSight overlay spike · {OverlayRules.Scale(rect.Height):F2}×";
    }

    private void Position()
    {
        if (positioning || handle == 0) return;
        positioning = true;
        try { styler.Position(handle, rect); }
        finally { positioning = false; }
    }
}
