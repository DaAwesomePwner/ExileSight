using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ExileSight.App.Ui;
using ExileSight.Platform.Abstractions.Input;
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
    private ShopCardSurface card = null!;
    private readonly FeatureViews views;
    private readonly OverlayTheme overlayTheme;
    private CardTheme? theme;
    private HostedOverlayWindow? window;
    private PixelRect? placement;
    private string? selectedAmount;
    public ShopView(ShopModel model, FeatureViews views, IServiceProvider services)
    {
        this.model = model; this.services = services; this.views = views;
        overlayTheme = new(services.GetRequiredService<ISettingsStore<SettingsDocument>>());
        HorizontalAlignment = HorizontalAlignment.Left;
        BuildCard();
        model.Opened += () => Dispatcher.BeginInvoke(Open);
        model.Changed += () => Dispatcher.BeginInvoke(Refresh);
        Refresh();
    }
    private void BuildCard()
    {
        theme = overlayTheme.Current;
        Children.Clear();
        card = new ShopCardSurface(model.CardSnapshot(DateTimeOffset.UtcNow, null), theme, (button, choice) =>
        {
            var acceptClick = false;
            button.PreviewMouseLeftButtonDown += (_, e) => acceptClick = ShopModel.AcceptClick(e.ClickCount);
            // Preserve the double-click guard before FeatureViews redeems the physical token.
            button.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (!acceptClick) e.Handled = true;
                else if (model.Suggest(choice) is { } next) selectedAmount = model.NoteMode
                    ? next.ToString() : GameAction.FormatAmount(next.Amount);
                acceptClick = false;
            };
            views.Bind(button, "vaalasync:" + choice);
        });
        Children.Add(card.Frame);
        card.History.Click += (_, _) => new HistoryView(model, services.GetRequiredService<PriceService>()).Show();
        card.EditCustom.Click += (_, _) => new HistoryView(model, services.GetRequiredService<PriceService>()).Show();
        card.Close.Click += (_, _) => services.GetRequiredService<OverlayHost>().SetVisible(window!, false);
    }
    public void Initialize()
    {
        window = (HostedOverlayWindow)Window.GetWindow(this);
        overlayTheme.Watch(window, Refresh);
        if (Parent is Border surface) { surface.Width = 330; surface.Height = double.NaN; }
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
        selectedAmount = null;
        Refresh();
        Measure(new Size(330, double.PositiveInfinity));
        var width = Math.Min(client.Width, (int)(330 * scale));
        var height = Math.Min(client.Height, (int)(DesiredSize.Height * scale));
        placement = new(Math.Clamp(cursor.X + 8, client.X, client.X + client.Width - width),
            Math.Clamp(cursor.Y + 8, client.Y, client.Y + client.Height - height), width, height);
        Refresh();
        services.GetRequiredService<OverlayHost>().SetVisible(window, true);
    }
    private void Position()
    {
        if (window?.IsVisible == true && placement is { } bounds)
        {
            Measure(new Size(330, double.PositiveInfinity));
            var client = services.GetRequiredService<IGameWindow>().Current.ClientRect;
            var scale = services.GetRequiredService<OverlayHost>().EffectiveScale;
            var height = Math.Min(client.Height, (int)Math.Ceiling(DesiredSize.Height * scale));
            bounds = new(bounds.X, Math.Clamp(bounds.Y, client.Y, client.Y + client.Height - height), bounds.Width, height);
            services.GetRequiredService<IOverlayWindowStyler>().Position(new WindowInteropHelper(window).Handle, bounds);
        }
    }
    private void Refresh()
    {
        if (!ReferenceEquals(theme, overlayTheme.Current)) BuildCard();
        card.Update(model.CardSnapshot(DateTimeOffset.UtcNow, selectedAmount));
        Position();
    }
}
