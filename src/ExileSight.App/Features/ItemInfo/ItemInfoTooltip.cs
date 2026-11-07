using System.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using ExileSight.Core.Features;
using ExileSight.Core.Input;
using ExileSight.Core.Overlay;
using ExileSight.Core.Settings;
using ExileSight.Features.ItemInfo;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Input;

namespace ExileSight.App.Features.ItemInfo;

public sealed class ItemInfoTooltip(PassiveOverlayInput passive, IGameWindow game,
    IOverlayWindowStyler styler, ISettingsStore<SettingsDocument> settings, ILogger<ItemInfoTooltip> logger) : IDisposable
{
    private readonly Window window = new()
    {
        WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
        ShowActivated = false, Focusable = false, Topmost = true, AllowsTransparency = true,
        Background = Brushes.Transparent, Width = 1, Height = 1
    };
    private readonly StackPanel panel = new() { Width = 410 };
    private ItemInfoViewModel? model;
    private nint handle;
    private int originX, originY;
    private double scale;
    private bool disposed;
    private bool dpiPending;
    public void Start(ItemInfoViewModel viewModel)
    {
        model = viewModel;
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/ExileSight.App;component/Features/ItemInfo/OverlayTheme.xaml", UriKind.Relative) });
        window.Content = new Border
        {
            Background = (Brush)window.FindResource("OverlayBackground"), Padding = new Thickness(10), Child = panel
        };
        handle = new WindowInteropHelper(window).EnsureHandle();
        styler.Apply(handle);
        styler.SetClickThrough(handle, true);
        window.DpiChanged += DpiChanged;
        model.PropertyChanged += Changed;
        game.Changed += EnvironmentChanged;
        settings.Changed += SettingsChanged;
    }
    private void Changed(object? sender, PropertyChangedEventArgs args) => window.Dispatcher.BeginInvoke(() =>
    {
        if (disposed) return;
        if (model?.Current is null) { Guard(Hide); return; }
        Guard(Open);
    });
    private void Open()
    {
        var state = game.Current;
        var client = state.ClientRect;
        if (!state.Found || !state.IsForeground || state.IsMinimized || !settings.Snapshot.Overlay.Enabled || client.Width <= 0 || client.Height <= 0)
        { Close(); return; }
        panel.Children.Clear();
        foreach (var section in model!.Sections)
        {
            panel.Children.Add(new TextBlock { Text = section.Title, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
            foreach (var row in section.Rows)
            {
                var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
                foreach (var part in row.Text) text.Inlines.Add(new Run(part.Text) { Foreground = Color(part.Tone) });
                panel.Children.Add(text);
            }
        }
        var cursor = HookHost.CursorPosition();
        originX = cursor.X; originY = cursor.Y;
        Position();
        window.Show();
        Position(); // WPF may adjust its HWND on first show.
        passive.Open(this, Observed);
    }
    private void Position()
    {
        var client = game.Current.ClientRect;
        if (client.Width <= 0 || client.Height <= 0) throw new InvalidOperationException("Game client has no drawable area");
        scale = OverlayRules.Scale(client.Height, settings.Snapshot.Overlay.ScaleOverride);
        panel.Measure(new Size(410, double.PositiveInfinity));
        var width = Math.Min((int)Math.Ceiling(430 * scale), client.Width);
        var height = Math.Min((int)Math.Ceiling((panel.DesiredSize.Height + 20) * scale), client.Height);
        var x = Math.Clamp(originX + (int)(12 * scale), client.X, client.X + client.Width - width);
        var y = Math.Clamp(originY + (int)(12 * scale), client.Y, client.Y + client.Height - height);
        styler.Position(handle, new(x, y, width, height));
        var dpi = VisualTreeHelper.GetDpi(window);
        ((FrameworkElement)window.Content).LayoutTransform = new ScaleTransform(scale / dpi.DpiScaleX, scale / dpi.DpiScaleY);
    }
    private Brush Color(TooltipTone tone) => (Brush)window.FindResource(tone switch
    {
        TooltipTone.Normal => "OverlayForeground", TooltipTone.Best => "OverlayAccent",
        _ => "Tooltip" + tone
    });
    private void Guard(Action action) => OverlayFailureGuard.Run(action, Close,
        error => logger.LogWarning("Item tooltip failed: {ExceptionType}", error.GetType().Name));
    private void DpiChanged(object sender, DpiChangedEventArgs args)
    {
        if (dpiPending) return;
        dpiPending = true;
        window.Dispatcher.BeginInvoke(() =>
        {
            dpiPending = false;
            if (!disposed && window.IsVisible) Guard(Position);
        });
    }
    private void Observed(PassiveInput input)
    {
        if (window.IsVisible && (input.Message == 0x100 || input.Message == 0x200 &&
            TooltipDismissal.MovedBeyond(input.X, input.Y, originX, originY, scale))) Close();
    }
    private void Hide() { passive.Close(this); window.Hide(); }
    private void Close() { try { Hide(); } finally { if (model?.Current is not null) model.Close(); } }
    private void EnvironmentChanged(object? sender, EventArgs args) => window.Dispatcher.BeginInvoke(() =>
    {
        if (disposed || !window.IsVisible) return;
        Guard(() =>
        {
            var state = game.Current;
            if (!state.Found || !state.IsForeground || state.IsMinimized || !settings.Snapshot.Overlay.Enabled) Close();
            else Position();
        });
    });
    private void SettingsChanged(object? sender, SettingsChangedEventArgs args) => EnvironmentChanged(sender, args);
    public void Dispose()
    {
        disposed = true;
        window.DpiChanged -= DpiChanged;
        if (model is not null) model.PropertyChanged -= Changed;
        game.Changed -= EnvironmentChanged;
        settings.Changed -= SettingsChanged;
        void Stop() { passive.Close(this); window.Close(); }
        if (window.Dispatcher.CheckAccess()) Stop(); else window.Dispatcher.Invoke(Stop);
    }
}
