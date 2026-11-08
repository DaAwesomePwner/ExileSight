using System.Windows;
using ExileSight.Core.Features;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ExileSight.App.Ui;
using ExileSight.App.Features.Macros;
using ExileSight.Core.Input;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using ExileSight.Core.Overlay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using ExileSight.Features.Omni;
using ExileSight.Platform.Abstractions.Shell;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Input;

namespace ExileSight.App.Features;

/// <summary>Pre-created, no-activate app window. Passive hook observations only dismiss UI.</summary>
public sealed class OmniMenuView(PassiveOverlayInput passive, IBrowser browser, IGameWindow game,
    IOverlayWindowStyler styler, ISettingsStore<SettingsDocument> settings, ILogger<OmniMenuView> logger, IHostApplicationLifetime lifetime, PriceService? prices = null) : IDisposable
{
    private FrameworkElement? card;
    private readonly Window window = new()
    {
        WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
        ShowActivated = false, Topmost = true, AllowsTransparency = true, Background = Brushes.Transparent,
        Width = 260, Height = 180
    };
    private OmniLeagueDiscovery? discovery;
    private OmniMenu? model;
    private nint handle;
    private PixelRect bounds;
    private volatile bool disposed;
    private bool dpiPending;
    private int originX, originY;
    public void Start(OmniMenu menu)
    {
        model = menu;
        window.Content = new Border();
        handle = new WindowInteropHelper(window).EnsureHandle();
        styler.Apply(handle);
        styler.SetClickThrough(handle, false);
        window.DpiChanged += DpiChanged;
        menu.League = ReadLeague;
        menu.CursorPosition = () => { var point = HookHost.CursorPosition(); return (point.X, point.Y); };
        menu.ReportFailure = type => logger.LogWarning("Omni menu failed: {ExceptionType}", type);
        menu.Presented += Open;
        window.IsVisibleChanged += VisibilityChanged;
        if (prices is not null)
        {
            discovery = new(() => prices.GetLeaguesAsync(cancellationToken: lifetime.ApplicationStopping),
                type => logger.LogWarning("League discovery failed: {ExceptionType}", type));
            _ = discovery.RefreshAsync();
        }
        game.Changed += GameChanged;
        settings.Changed += SettingsChanged;
    }
    private string ReadLeague()
    {
        if (!disposed && !lifetime.ApplicationStopping.IsCancellationRequested) _ = discovery?.RefreshAsync();
        return OmniLeague.Select(settings.Snapshot.Prices.League, discovery?.Result);
    }
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (window.IsVisible) passive.Open(this, Observed);
        else passive.Close(this);
    }
    private void Open(OmniMenuPresentation presentation) => window.Dispatcher.BeginInvoke(() =>
    {
        if (model?.IsCurrent(presentation.Origin) != true) return;
        Guard(() => OpenCore(presentation));
    });
    private void Guard(Action action) => OverlayFailureGuard.Run(action,
        () => { passive.Close(this); window.Hide(); },
        error => logger.LogWarning("Omni menu failed: {ExceptionType}", error.GetType().Name));
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
    private void OpenCore(OmniMenuPresentation presentation)
    {
        if (disposed || !game.Current.Found || game.Current.IsMinimized || !game.Current.IsForeground || !settings.Snapshot.Overlay.Enabled) return;
        card = OmniMenuBuilder.Build(presentation, CardTheme.Default);
        ((Border)window.Content).Child = card;
        foreach (var button in OnDemandControls.Buttons(card))
            button.Click += (_, _) =>
            {
                window.Hide();
                try
                {
                    if (button.Tag is OmniLink link) OmniMenu.Open(browser, link);
                    else if (button.Tag is ItemMenuEntry entry) entry.Execute();
                }
                catch (Exception error)
                {
                    if (button.Tag is OmniLink) logger.LogWarning("Browser open failed: {ExceptionType}", error.GetType().Name);
                    else logger.LogWarning("Item menu action failed: {ExceptionType}", error.GetType().Name);
                }
            };
        originX = presentation.Origin.X; originY = presentation.Origin.Y;
        Position();
        window.Show();
        Position();
        passive.Open(this, Observed);
    }
    private void Position()
    {
        var client = game.Current.ClientRect;
        if (client.Width <= 0 || client.Height <= 0) throw new InvalidOperationException("Game client has no drawable area");
        var scale = OverlayRules.Scale(client.Height, settings.Snapshot.Overlay.ScaleOverride);
        card?.Measure(new Size(260, double.PositiveInfinity));
        bounds = PopupPlacement.Place(client, styler.GetWorkArea(client), originX, originY,
            260, Math.Max(80, card?.DesiredSize.Height ?? 80), scale, 8);
        styler.Position(handle, bounds);
        var dpi = VisualTreeHelper.GetDpi(window);
        ((FrameworkElement)window.Content).LayoutTransform = new ScaleTransform(
            OverlayRefresh.ContentScale(scale, dpi.DpiScaleX), OverlayRefresh.ContentScale(scale, dpi.DpiScaleY));
    }
    private void Observed(PassiveInput input)
    {
        if (disposed || !window.IsVisible) return;
        var inside = input.X >= bounds.X && input.X < (bounds.X + bounds.Width) && input.Y >= bounds.Y && input.Y < (bounds.Y + bounds.Height);
        var far = input.X < bounds.X - 80 || input.X > (bounds.X + bounds.Width) + 80 ||
            input.Y < bounds.Y - 80 || input.Y > (bounds.Y + bounds.Height) + 80;
        if (input.Message == 0x100 || (input.Message is 0x201 or 0x204 or 0x207 or 0x20b && !inside) ||
            (input.Message == 0x200 && far)) window.Hide();
    }
    private void SettingsChanged(object? sender, SettingsChangedEventArgs args) => window.Dispatcher.BeginInvoke(() =>
    {
        if (!disposed && !settings.Snapshot.Overlay.Enabled) window.Hide();
    });
    private void GameChanged(object? sender, EventArgs args) => window.Dispatcher.BeginInvoke(() =>
    {
        if (disposed || !window.IsVisible) return;
        Guard(() =>
        {
            var state = game.Current;
            if (!state.Found || !state.IsForeground || state.IsMinimized) window.Hide();
            else Position();
        });
    });
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        window.DpiChanged -= DpiChanged;

        game.Changed -= GameChanged;
        settings.Changed -= SettingsChanged;
        if (model is not null) model.Presented -= Open;
        void Close()
        {
            passive.Close(this);
            window.IsVisibleChanged -= VisibilityChanged;
            window.Close();
        }
        if (window.Dispatcher.CheckAccess()) Close();
        else window.Dispatcher.BeginInvoke(Close);
    }
}
