using System.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ExileSight.Core.Features;
using ExileSight.App.Ui;
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
    private readonly ContentControl content = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private ItemInfoViewModel? model;
    private nint handle;
    private int originX, originY;
    private double scale;
    private bool disposed;
    private bool dpiPending;
    private ItemCard? renderedCard;
    private CardTheme? renderedTheme;
    private string? renderedNotice;
    private double renderedCap;
    public void Start(ItemInfoViewModel viewModel)
    {
        model = viewModel;
        window.Content = content;
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
        var overlay = settings.Snapshot.Overlay;
        var theme = CardTheme.For(overlay.Density, overlay.Contrast);
        var cap = client.Height * .8 / scale;
        if (!ReferenceEquals(renderedCard, model!.Card) || !ReferenceEquals(renderedTheme, theme) ||
            renderedCap != cap || renderedNotice != model.Notice)
        {
            content.Content = ItemCardView.Build(model.Card!, theme, cap, model.Notice);
            renderedCard = model.Card; renderedTheme = theme; renderedCap = cap; renderedNotice = model.Notice;
        }
        var card = (FrameworkElement)content.Content;
        card.Measure(new Size(theme.CardWidth, double.PositiveInfinity));
        var bounds = PopupPlacement.PlaceCard(client, styler.GetWorkArea(new(originX, originY, 1, 1)), originX, originY,
            theme.CardWidth, card.DesiredSize.Height, scale, overlay.ToCardPlacementOptions());
        styler.Position(handle, bounds);
        var dpi = VisualTreeHelper.GetDpi(window);
        ((FrameworkElement)window.Content).LayoutTransform = new ScaleTransform(
            OverlayRefresh.ContentScale(scale, dpi.DpiScaleX), OverlayRefresh.ContentScale(scale, dpi.DpiScaleY));
    }
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
    private void Hide()
    {
        passive.Close(this); window.Hide();
        content.Content = null; renderedCard = null; renderedTheme = null; renderedNotice = null;
    }
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
