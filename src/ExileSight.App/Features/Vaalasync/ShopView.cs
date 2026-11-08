using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ExileSight.App.Overlay;
using ExileSight.Core.Pricing;
using ExileSight.Features.Vaalasync;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Vaalasync;

public sealed class ShopView : StackPanel
{
    private readonly ShopModel model;
    private readonly IServiceProvider services;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White };
    private readonly TextBlock price = new() { Foreground = Brushes.White };
    private readonly List<(Button Button, string Choice)> buttons = [];
    private HostedOverlayWindow? window;
    private PixelRect? placement;
    public ShopView(ShopModel model, FeatureViews views, IServiceProvider services)
    {
        this.model = model; this.services = services;
        Background = Brushes.Black;
        Children.Add(new TextBlock { Text = "Shop prices", Foreground = Brushes.White, FontSize = 20 });
        Children.Add(price); Children.Add(status);
        foreach (var (label, choice) in new[] { ("−1", "minus1"), ("+1", "plus1"), ("−10%", "minus10"), ("+10%", "plus10"), ("Apply custom", "custom") })
        {
            var button = new Button { Content = label, Margin = new Thickness(2), Focusable = false };
            var acceptClick = false;
            button.PreviewMouseLeftButtonDown += (_, e) => acceptClick = ShopModel.AcceptClick(e.ClickCount);
            // Runs before FeatureViews' mouse-up handler, so a double click cannot redeem a token.
            button.PreviewMouseLeftButtonUp += (_, e) => { if (!acceptClick) e.Handled = true; acceptClick = false; };
            Children.Add(button); buttons.Add((button, choice)); views.Bind(button, "vaalasync:" + choice);
        }
        var history = new Button { Content = "History / custom price / record purchase" };
        history.Click += (_, _) => new HistoryView(model, services.GetRequiredService<PriceService>()).Show();
        Children.Add(history);
        var close = new Button { Content = "Close" };
        close.Click += (_, _) => services.GetRequiredService<OverlayHost>().SetVisible(window!, false);
        Children.Add(close);
        model.Opened += () => Dispatcher.BeginInvoke(Open);
        model.Changed += () => Dispatcher.BeginInvoke(Refresh);
    }
    public void Initialize()
    {
        window = (HostedOverlayWindow)Window.GetWindow(this);
        services.GetRequiredService<OverlayHost>().SetVisible(window, false);
        services.GetRequiredService<OverlayHost>().Changed += (_, _) => Position();
    }
    private void Open()
    {
        if (window is null) return;
        var game = services.GetRequiredService<IGameWindow>().Current;
        if (!game.Found || !game.IsForeground || game.ClientRect.Width <= 0 || game.ClientRect.Height <= 0) return;
        var cursor = HookHost.CursorPosition();
        var scale = services.GetRequiredService<OverlayHost>().EffectiveScale;
        var client = game.ClientRect;
        var width = Math.Min(client.Width, (int)(400 * scale));
        var height = Math.Min(client.Height, (int)(300 * scale));
        placement = new(Math.Clamp(cursor.X + 8, client.X, client.X + client.Width - width),
            Math.Clamp(cursor.Y + 8, client.Y, client.Y + client.Height - height), width, height);
        Refresh();
        services.GetRequiredService<OverlayHost>().SetVisible(window, true);
    }
    private void Position()
    {
        if (window?.IsVisible == true && placement is { } bounds)
            services.GetRequiredService<IOverlayWindowStyler>().Position(new WindowInteropHelper(window).Handle, bounds);
    }
    private void Refresh()
    {
        price.Text = model.Note?.ToString(); status.Text = model.Status + "\n" + model.CurrencyReminder;
        foreach (var (button, choice) in buttons)
        {
            var suggestion = model.Suggest(choice);
            button.IsEnabled = model.CanApply && suggestion is not null;
            button.Content = $"{choice}: {(suggestion is null ? "unavailable" : $"{suggestion.Amount} {suggestion.Currency}")}";
        }
    }
}
