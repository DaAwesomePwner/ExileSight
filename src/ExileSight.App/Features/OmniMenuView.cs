using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ExileSight.Core.Settings;
using ExileSight.Core.Overlay;
using Microsoft.Extensions.Logging;
using ExileSight.Features.Omni;
using ExileSight.Platform.Abstractions.Shell;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Input;

namespace ExileSight.App.Features;

/// <summary>Pre-created, no-activate app window. Passive hook observations only dismiss UI.</summary>
public sealed class OmniMenuView(HookHost hooks, IBrowser browser, IGameWindow game,
    IOverlayWindowStyler styler, ISettingsStore<SettingsDocument> settings, ILogger<OmniMenuView> logger) : IDisposable
{
    private readonly StackPanel panel = new();
    private readonly Window window = new()
    {
        WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
        ShowActivated = false, Topmost = true, AllowsTransparency = true, Background = Brushes.Transparent,
        Width = 260, Height = 180
    };
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
        hooks.Observed += Observed;
        game.Changed += GameChanged;
        settings.Changed += SettingsChanged;
    }
    private string? ReadLeague()
    {
        // Works with preserved extension data today and the parallel task's typed Prices section.
        var document = JsonSerializer.SerializeToElement(settings.Snapshot,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        if (document.TryGetProperty("prices", out var prices) && prices.ValueKind == JsonValueKind.Object &&
            prices.TryGetProperty("league", out var league) && league.ValueKind == JsonValueKind.String)
            return league.GetString();
        return null;
    }
    private void Open(IReadOnlyList<OmniLink> links) => window.Dispatcher.BeginInvoke(() =>
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
    });
    private void Observed(PassiveInput input) => window.Dispatcher.BeginInvoke(() =>
    {
        if (disposed || !window.IsVisible) return;
        var inside = input.X >= bounds.X && input.X < (bounds.X + bounds.Width) && input.Y >= bounds.Y && input.Y < (bounds.Y + bounds.Height);
        var far = input.X < bounds.X - 80 || input.X > (bounds.X + bounds.Width) + 80 ||
            input.Y < bounds.Y - 80 || input.Y > (bounds.Y + bounds.Height) + 80;
        if (input.Message == 0x100 || (input.Message is 0x201 or 0x204 or 0x207 && !inside) ||
            (input.Message == 0x200 && far)) window.Hide();
    });
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
        hooks.Observed -= Observed;
        game.Changed -= GameChanged;
        settings.Changed -= SettingsChanged;
        if (model is not null) model.Opened -= Open;
        if (window.Dispatcher.CheckAccess()) window.Close();
        else window.Dispatcher.BeginInvoke(window.Close);
    }
}
