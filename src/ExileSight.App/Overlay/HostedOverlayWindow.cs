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
    internal Func<PixelRect, double, PixelRect>? PhysicalPlacement { get; set; }
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
        var physical = PhysicalPlacement?.Invoke(client, OverlayRules.Scale(client.Height, scaleOverride))
            ?? placement.Calculate(client, design, scaleOverride);
        bool justShown = !IsVisible;
        if (justShown) Show();
        var scale = OverlayRules.Scale(client.Height, scaleOverride);
        OverlayRefresh Decide()
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return OverlayRefresh.Decide(physical, styler.GetBounds(handle), scale,
                dpi.DpiScaleX, dpi.DpiScaleY, contentScale.ScaleX, contentScale.ScaleY, justShown);
        }
        if (Decide().Reposition)
        {
            // Correct WPF's DPI-suggested rect whenever it differs from our physical geometry.
            // Show also requires reasserting HWND_TOPMOST, even when geometry already matches.
            styler.Position(handle, physical);
        }
        // Native placement establishes the destination monitor before converting content to DIPs.
        var refresh = Decide();
        if (refresh.Rescale)
        {
            contentScale.ScaleX = refresh.ScaleX;
            contentScale.ScaleY = refresh.ScaleY;
        }
    }
}
