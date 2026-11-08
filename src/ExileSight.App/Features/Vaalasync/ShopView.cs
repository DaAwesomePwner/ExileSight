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
    private readonly FeatureViews views;
    private HostedOverlayWindow? window;
    private PixelRect? placement;
    private string? selectedAmount;
    public ShopView(ShopModel model, FeatureViews views, IServiceProvider services)
    {
        this.model = model; this.services = services; this.views = views;
        HorizontalAlignment = HorizontalAlignment.Left;
        model.Opened += () => Dispatcher.BeginInvoke(Open);
        model.Changed += () => Dispatcher.BeginInvoke(Refresh);
        Refresh();
    }
    public void Initialize()
    {
        window = (HostedOverlayWindow)Window.GetWindow(this);
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
        var amounts = new List<ShopAmount>();
        foreach (var (label, choice) in new[] { ("−10%", "minus10"), ("−1", "minus1"), ("+1", "plus1"), ("+10%", "plus10") })
        {
            var suggestion = model.Suggest(choice);
            // A duplicate still shows its number, but only Suggest decides availability.
            var display = suggestion;
            if (display is null && !model.NoteMode && model.Note is { } note)
            {
                var raw = choice switch { "minus10" => note.Amount * .9m, "minus1" => note.Amount - 1,
                    "plus1" => note.Amount + 1, _ => note.Amount * 1.1m };
                display = note with { Amount = Math.Max(1, decimal.Round(raw, 0, MidpointRounding.AwayFromZero)) };
            }
            amounts.Add(new(choice, label, display is null ? "—" : model.NoteMode
                ? display.ToString() : GameAction.FormatAmount(display.Amount), suggestion is not null));
        }
        var pasted = !model.CanApply && model.Status.Contains(ShopModel.RearmMessage, StringComparison.Ordinal)
            ? selectedAmount : null;
        var notice = model.DisplayStatus;
        if (notice == model.Instructions || notice == ShopModel.RearmMessage) notice = "";
        var data = new ShopCardModel(model.CurrentItemName ?? "Shop pricer", model.Note?.DisplayAmount,
            model.FirstListedAt is { } first ? DateTimeOffset.UtcNow - first : null,
            model.Currency, model.CanApply, model.NoteMode, amounts,
            model.Suggest("custom") is { } custom ? model.NoteMode ? custom.ToString() : custom.DisplayAmount : null,
            pasted, notice);
        Children.Clear();
        var card = ShopCardBuilder.Build(data, CardTheme.Default);
        Children.Add(card); // Bind requires the control to belong to the existing window.
        foreach (var button in Buttons(card))
        {
            var choice = (string)button.Tag;
            if (choice is "history" or "edit-custom")
                button.Click += (_, _) => new HistoryView(model, services.GetRequiredService<PriceService>()).Show();
            else if (choice == "close")
                button.Click += (_, _) => services.GetRequiredService<OverlayHost>().SetVisible(window!, false);
            else
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
            }
        }
        Position();
    }

    private static IEnumerable<Button> Buttons(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is Button button) yield return button;
            else foreach (var descendant in Buttons(child)) yield return descendant;
        }
    }
}
