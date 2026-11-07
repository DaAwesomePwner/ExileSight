using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
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
    private readonly StackPanel panel = new();
    private readonly Window window = new()
    {
        WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
        ShowActivated = false, Topmost = true, AllowsTransparency = true, Background = Brushes.Transparent,
        Width = 260, Height = 180
    };
    private LeagueResult? discovered;
    private OmniMenu? model;
    private nint handle;
    private PixelRect bounds;
    private volatile bool disposed;
    public void Start(OmniMenu menu)
    {
        model = menu;
        window.Content = new Border { Background = Brushes.Black, Padding = new Thickness(8), Child = panel };
        handle = new WindowInteropHelper(window).EnsureHandle();
        styler.Apply(handle);
        styler.SetClickThrough(handle, false);
        menu.League = ReadLeague;
        menu.Opened += Open;
        window.IsVisibleChanged += VisibilityChanged;
        if (prices is not null) _ = DiscoverLeagueAsync();
        game.Changed += GameChanged;
        settings.Changed += SettingsChanged;
    }
    private string ReadLeague() => OmniLeague.Select(settings.Snapshot.Prices.League, Volatile.Read(ref discovered));
    private async Task DiscoverLeagueAsync()
    {
        try { Volatile.Write(ref discovered, await prices!.GetLeaguesAsync(cancellationToken: lifetime.ApplicationStopping).ConfigureAwait(false)); }
        catch (Exception error) { logger.LogWarning("League discovery failed: {ExceptionType}", error.GetType().Name); }
    }
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (window.IsVisible) passive.Open(this, Observed);
        else passive.Close(this);
    }
    private void Open(IReadOnlyList<OmniLink> links) => window.Dispatcher.BeginInvoke(() =>
    {
        try { OpenCore(links); }
        catch (Exception error)
        {
            window.Hide();
            logger.LogWarning("Omni menu open failed: {ExceptionType}", error.GetType().Name);
        }
    });
    private void OpenCore(IReadOnlyList<OmniLink> links)
    {
        if (disposed || !game.Current.Found || game.Current.IsMinimized || !game.Current.IsForeground || !settings.Snapshot.Overlay.Enabled) return;
        panel.Children.Clear();
        foreach (var link in links)
        {
            var button = new Button { Content = link.Label, Margin = new Thickness(2), Padding = new Thickness(6) };
            button.Click += (_, _) =>
            {
                window.Hide();
                try { OmniMenu.Open(browser, link); }
                catch (Exception error) { logger.LogWarning("Browser open failed: {ExceptionType}", error.GetType().Name); }
            };
            panel.Children.Add(button);
        }
        var cursor = HookHost.CursorPosition();
        var client = game.Current.ClientRect;
        if (client.Width <= 0 || client.Height <= 0) return;
        var scale = OverlayRules.Scale(client.Height, settings.Snapshot.Overlay.ScaleOverride);
        var width = Math.Min((int)(260 * scale), client.Width);
        var height = Math.Min((int)(180 * scale), client.Height);
        var x = Math.Clamp(cursor.X + 8, client.X, (client.X + client.Width) - width);
        var y = Math.Clamp(cursor.Y + 8, client.Y, (client.Y + client.Height) - height);
        bounds = new(x, y, width, height);
        window.Show();
        styler.Position(handle, bounds);
        var dpi = VisualTreeHelper.GetDpi(window);
        ((FrameworkElement)window.Content).LayoutTransform = new ScaleTransform(scale / dpi.DpiScaleX, scale / dpi.DpiScaleY);
        passive.Open(this, Observed);
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
    private void GameChanged(object? sender, EventArgs args)
    {
        if (!game.Current.IsForeground) window.Hide();
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        game.Changed -= GameChanged;
        settings.Changed -= SettingsChanged;
        if (model is not null) model.Opened -= Open;
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
