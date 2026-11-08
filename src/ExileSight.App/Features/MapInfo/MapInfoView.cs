using Microsoft.Extensions.Logging;
using ExileSight.App.Ui;
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
    private readonly ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private MapInfoModel? model;
    private nint handle;
    private int originX, originY;
    private double scale;
    private bool disposed;
    private bool dpiPending;
    public void Start(MapInfoModel viewModel)
    {
        model = viewModel;
        window.Content = scroll;
        scroll.AddHandler(Button.ClickEvent, new RoutedEventHandler((_, args) =>
        {
            if (args.OriginalSource is Button { Tag: string id }) model.Cycle(id);
            else if (window.IsVisible) Guard(Position);
        }));
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
        var result = model?.Current;
        if (result is null) { Guard(Hide); return; }
        Guard(() => Open(result));
    });
    private void Open(MapResult result)
    {
        var state = game.Current;
        var client = state.ClientRect;
        if (!state.Found || !state.IsForeground || state.IsMinimized || !settings.Snapshot.Overlay.Enabled || client.Width <= 0 || client.Height <= 0)
        { Close(); return; }
        var sections = settings.Snapshot.Features.Sections;
        var thresholds = sections.TryGetValue("mapinfo", out var section) && section.TryGetValue("settings", out var value)
            ? MapInfoSettings.Read(value) : new MapInfoSettings();
        var card = MapCardBuilder.Build(new(result, thresholds, model!.Notice), CardTheme.Default);
        card.Width = double.NaN; // Let a visible scrollbar reserve space without clipping the text.
        scroll.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        scroll.Content = card;
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
        var card = (FrameworkElement)scroll.Content;
        card.Measure(new Size(CardTheme.Default.CardWidth, double.PositiveInfinity));
        var bounds = PopupPlacement.PlaceCard(client, styler.GetWorkArea(new(originX, originY, 1, 1)), originX, originY,
            CardTheme.Default.CardWidth, Math.Min(card.DesiredSize.Height, client.Height * .8 / scale), scale,
            settings.Snapshot.Overlay.ToCardPlacementOptions());
        styler.Position(handle, bounds);
        var dpi = VisualTreeHelper.GetDpi(window);
        ((FrameworkElement)window.Content).LayoutTransform = new ScaleTransform(
            OverlayRefresh.ContentScale(scale, dpi.DpiScaleX), OverlayRefresh.ContentScale(scale, dpi.DpiScaleY));
    }
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
    private void SettingsChanged(object? sender, SettingsChangedEventArgs args) => window.Dispatcher.BeginInvoke(() =>
    { if (!disposed && window.IsVisible && model?.Current is { } result) Guard(() => Open(result)); });
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
