using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ExileSight.App.Overlay;
using ExileSight.Core.Input;
using ExileSight.Features.Macros;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ExileSight.Platform.Windows.Input;

namespace ExileSight.App.Features.Macros;

/// <summary>Hosted windows supply NOACTIVATE and DPI scaling; selections redeem physical clicks only.</summary>
public sealed class MacroWheelView : Canvas
{
    private readonly MacroWheel model;
    private readonly OverlayHost host;
    private readonly PassiveOverlayInput passive;
    private readonly IGameWindow game;
    private readonly ILogger<MacroWheelView> logger;
    private readonly Button[] slots = new Button[8];
    private HostedOverlayWindow? window;
    private long presentedVersion;
    public MacroWheelView(MacroWheel model, FeatureViews views, IServiceProvider services)
    {
        this.model = model;
        host = services.GetRequiredService<OverlayHost>();
        passive = services.GetRequiredService<PassiveOverlayInput>();
        game = services.GetRequiredService<IGameWindow>();
        logger = services.GetRequiredService<ILogger<MacroWheelView>>();
        Background = Brushes.Black;
        for (var i = 0; i < 8; i++)
        {
            var slot = i;
            var button = slots[i] = new Button { Width = 96, Height = 46, Focusable = false };
            var angle = i * Math.PI / 4 - Math.PI / 2;
            SetLeft(button, 152 + Math.Cos(angle) * 142);
            SetTop(button, 127 + Math.Sin(angle) * 116);
            button.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (!model.IsCurrent(presentedVersion) || !button.IsMouseOver || !button.IsEnabled || window is null) return;
                var point = button.PointToScreen(e.GetPosition(button));
                var token = services.GetRequiredService<HookHost>().RedeemOverlayClick(
                    new WindowInteropHelper(window).Handle, (int)point.X, (int)point.Y, "macros:slot" + slot);
                var version = presentedVersion;
                if (token is not null) _ = Task.Run(() =>
                {
                    try { model.SelectSlot(slot, version, token); }
                    catch (Exception error) { logger.LogWarning("Macro selection failed: {ExceptionType}", error.GetType().Name); }
                });
            };
            Children.Add(button);

        }
        var edit = new Button { Content = "Edit commands", Width = 108, Focusable = false };
        SetLeft(edit, 146); SetTop(edit, 135); Children.Add(edit);
        edit.Click += (_, _) => { Dismiss(); new MacroSettingsWindow(model).Show(); };
        model.Opened += Open;
        model.Closed += Close;
    }
    public void Initialize()
    {
        window = (HostedOverlayWindow)Window.GetWindow(this);
        host.SetVisible(window, false);
        window.IsVisibleChanged += VisibilityChanged;
        window.Closed += (_, _) =>
        {
            model.Opened -= Open; model.Closed -= Close;
            passive.Close(this);
        };
    }
    private void Guard(Action action) => OverlayFailureGuard.Run(action,
        () => { passive.Close(this); if (window is not null) host.SetVisible(window, false); model.Reset(); },
        error => logger.LogWarning("Macro wheel failed: {ExceptionType}", error.GetType().Name));
    private void Open(long id, IReadOnlyList<string> commands) => Dispatcher.BeginInvoke(() => Guard(() =>
    {
        if (window is null || !model.IsCurrent(id)) return;
        var state = game.Current;
        if (!state.Found || !state.IsForeground || state.IsMinimized) { model.Reset(); return; }
        presentedVersion = id;
        for (var i = 0; i < 8; i++)
        {
            slots[i].Content = new TextBlock { Text = commands[i].Length == 0 ? "Empty" : commands[i],
                TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap };
            slots[i].ToolTip = commands[i];
            slots[i].IsEnabled = !string.IsNullOrWhiteSpace(commands[i]);
        }
        host.SetVisible(window, true);
        if (!window.IsVisible) model.Reset();
    }));
    private void Close() => Dispatcher.BeginInvoke(() =>
    {
        if (window is not null && !model.IsCurrent(presentedVersion)) Guard(() => host.SetVisible(window, false));
    });
    private void Dismiss() { model.Reset(); if (window is not null) host.SetVisible(window, false); }
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (window!.IsVisible) passive.Open(this, Observed);
        else { passive.Close(this); if (model.IsCurrent(presentedVersion)) model.Reset(); }
    }
    private void Observed(PassiveInput input)
    {
        if (window?.IsVisible != true) return;
        var origin = PointToScreen(new Point());
        var end = PointToScreen(new Point(ActualWidth, ActualHeight));
        var inside = input.X >= origin.X && input.X < end.X && input.Y >= origin.Y && input.Y < end.Y;
        if (input.Message == 0x100 || (!inside && input.Message is 0x201 or 0x204 or 0x207 or 0x20b))
            Guard(Dismiss);
    }
}
