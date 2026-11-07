using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ExileSight.Core.Input;
using ExileSight.Core.Overlay;
using ExileSight.Core.Settings;
using ExileSight.Features.MapInfo;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Input;

namespace ExileSight.App.Features.MapInfo;

public sealed class MapInfoView(PassiveOverlayInput passive, IGameWindow game,
    IOverlayWindowStyler styler, ISettingsStore<SettingsDocument> settings, ILogger<MapInfoView> logger) : IDisposable
{
    private readonly Window window = new()
    {
        WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
        ShowActivated = false, Focusable = false, Topmost = true, AllowsTransparency = true,
        Background = Brushes.Transparent, Width = 1, Height = 1
    };
    private readonly StackPanel panel = new() { Width = 410 };
    private MapInfoModel? model;
    private nint handle;
    private int originX, originY;
    private double scale;
    private bool disposed;
    private bool dpiPending;
    public void Start(MapInfoModel viewModel)
    {
        model = viewModel;
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/ExileSight.App;component/Features/ItemInfo/OverlayTheme.xaml", UriKind.Relative) });
        window.Content = new Border
        {
            Background = (Brush)window.FindResource("OverlayBackground"), Padding = new Thickness(10), Child = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        handle = new WindowInteropHelper(window).EnsureHandle();
        styler.Apply(handle);
        styler.SetClickThrough(handle, false);
        window.DpiChanged += DpiChanged;
        model.Changed += Changed;
        game.Changed += EnvironmentChanged;
        settings.Changed += SettingsChanged;
    }
    private void Changed() => window.Dispatcher.BeginInvoke(() =>
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
        var result = model!.Current!;
        panel.Children.Add(Label(result.Name, Brushes.White));
        panel.Children.Add(Label($"Quantity: {result.Quantity?.ToString() ?? "?"}%   Rarity: {result.Rarity?.ToString() ?? "?"}%   Pack size: {result.PackSize?.ToString() ?? "?"}%", Brushes.Gold));
        panel.Children.Add(Label("Click a modifier to cycle danger 0–3", Brushes.LightGray));
        foreach (var row in result.Mods)
        {
            var text = $"[{row.Rank}] {row.Area}{(row.Area is null ? "" : ": ")}{row.Text}";
            var brush = row.Rank switch { 0 => Brushes.LightGray, 1 => Brushes.Yellow, 2 => Brushes.Orange, _ => Brushes.OrangeRed };
            var button = new Button { Content = Label(text, brush), Focusable = false, IsEnabled = row.Id is not null,
                HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Margin = new Thickness(0, 2, 0, 0) };
            if (row.Id is { } id) button.Click += (_, _) => model.Cycle(id);
            panel.Children.Add(button);
        }
        if (model.Notice is { } notice) panel.Children.Add(Label(notice, Brushes.Orange));
        if (!window.IsVisible)
        { var cursor = HookHost.CursorPosition(); originX = cursor.X; originY = cursor.Y; }
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
    private static TextBlock Label(string text, Brush color) => new()
    { Text = text, Foreground = color, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2) };
    private void Guard(Action action) => OverlayFailureGuard.Run(action, Close,
        error => logger.LogWarning("Map panel failed: {ExceptionType}", error.GetType().Name));
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
        if (!window.IsVisible) return;
        var bounds = styler.GetBounds(handle);
        var outside = input.X < bounds.X || input.Y < bounds.Y || input.X >= bounds.X + bounds.Width || input.Y >= bounds.Y + bounds.Height;
        if (input.Message == 0x100 || outside && input.Message is 0x201 or 0x204 or 0x207 or 0x20b) Close();
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
        if (model is not null) model.Changed -= Changed;
        game.Changed -= EnvironmentChanged;
        settings.Changed -= SettingsChanged;
        void Stop() { passive.Close(this); window.Close(); }
        if (window.Dispatcher.CheckAccess()) Stop(); else window.Dispatcher.Invoke(Stop);
    }
}
