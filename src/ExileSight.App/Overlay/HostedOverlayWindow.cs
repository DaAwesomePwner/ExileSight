using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ExileSight.Core.Overlay;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Overlay;

/// <summary>Created during host setup, then reused. All operations belong to the UI dispatcher.</summary>
public sealed class HostedOverlayWindow : System.Windows.Window
{
    private readonly IOverlayWindowStyler styler;
    private readonly FrameworkElement surface;
    private readonly OverlayRect design;
    private readonly ScaleTransform contentScale = new();
    private readonly OverlayPlacement placement = new();
    private bool dpiRefreshPending;
    private nint handle;
    private bool clickThrough;
    internal bool RequestedVisible { get; set; } = true;
    internal event EventHandler? RefreshRequested;

    internal HostedOverlayWindow(IOverlayWindowStyler styler, FrameworkElement content,
        OverlayRect design, bool clickThrough)
    {
        this.styler = styler;
        this.design = design;
        this.clickThrough = clickThrough;
        AllowsTransparency = true;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        Focusable = false;
        Background = Brushes.Transparent;
        Width = Height = 1;
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/ExileSight.App;component/Overlay/OverlayTheme.xaml", UriKind.Relative)
        });
        surface = new Border { Width = design.Width, Height = design.Height, Child = content,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        surface.LayoutTransform = contentScale;
        Content = surface;
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            styler.Apply(handle);
            styler.SetClickThrough(handle, this.clickThrough);
        };
        DpiChanged += (_, _) =>
        {
            if (dpiRefreshPending) return;
            dpiRefreshPending = true;
            Dispatcher.BeginInvoke(() =>
            {
                dpiRefreshPending = false;
                if (IsVisible) RefreshRequested?.Invoke(this, EventArgs.Empty);
            });
        };
        new WindowInteropHelper(this).EnsureHandle();
    }

    public void SetClickThrough(bool enabled)
    {
        Dispatcher.VerifyAccess();
        styler.SetClickThrough(handle, enabled);
        clickThrough = enabled;
    }

    internal void Update(GameWindowState state, bool enabled, double? scaleOverride)
    {
        Dispatcher.VerifyAccess();
        var client = state.ClientRect;
        if (!OverlayRules.IsVisible(state.Found, state.IsForeground, state.IsMinimized, enabled, RequestedVisible)
            || client.Width <= 0 || client.Height <= 0)
        {
            Hide();
            return;
        }
        var physical = placement.Calculate(client, design, scaleOverride);
        if (!IsVisible) Show();
        if (placement.NeedsPosition(physical))
        {
            // Record only successful placement. DPI-triggered refreshes with the same desired
            // bounds do not set native bounds again, preventing cross-monitor feedback loops.
            styler.Position(handle, physical);
            placement.Applied(physical);
        }
        // Native placement establishes the destination monitor before converting content to DIPs.
        var dpi = VisualTreeHelper.GetDpi(this);
        var scale = OverlayRules.Scale(client.Height, scaleOverride);
        contentScale.ScaleX = scale / dpi.DpiScaleX;
        contentScale.ScaleY = scale / dpi.DpiScaleY;
    }
}
