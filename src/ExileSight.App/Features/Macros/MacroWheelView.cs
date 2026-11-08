using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ExileSight.App.Ui;
using ExileSight.App.Overlay;
using ExileSight.App.Input;
using ExileSight.Core.Features;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Core.Input;
using ExileSight.Core.Overlay;
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
    private Button[] slots = [];
    private readonly Action<Button, int> wireSlot;
    private readonly Action editCommands;
    private HostedOverlayWindow? window;
    private long presentedVersion;
    private WheelOrigin origin = new(0, 0);
    private readonly IOverlayWindowStyler styler;
    public MacroWheelView(MacroWheel model, FeatureViews views, IServiceProvider services)
    {
        this.model = model;
        styler = services.GetRequiredService<IOverlayWindowStyler>();
        model.CursorPosition = () => { var p = HookHost.CursorPosition(); return new(p.X, p.Y); };
        var diagnostics = services.GetRequiredService<InputDiagnostics>();
        void Report(int slot, ActionResult result) => diagnostics.Report(
            new("macros:slot" + slot, 0, KeyModifiers.None, false, null,
                FeatureCommand: "macros:slot" + slot, FeatureId: "macros", CommandAction: ActionKind.SendChatCommand), result);
        host = services.GetRequiredService<OverlayHost>();
        passive = services.GetRequiredService<PassiveOverlayInput>();
        game = services.GetRequiredService<IGameWindow>();
        logger = services.GetRequiredService<ILogger<MacroWheelView>>();
        wireSlot = (button, slot) =>
        {
            button.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (!model.IsCurrent(presentedVersion) || !button.IsMouseOver || !button.IsEnabled || window is null) return;
                var point = button.PointToScreen(e.GetPosition(button));
                var token = services.GetRequiredService<HookHost>().RedeemOverlayClick(
                    new WindowInteropHelper(window).Handle, (int)point.X, (int)point.Y, "macros:slot" + slot);
                var version = presentedVersion;
                if (token is not null) _ = Task.Run(() =>
                {
                    try { Report(slot, model.SelectSlot(slot, version, token)); }
                    catch (Exception error) { Report(slot, new(ActionOutcome.Failed, "Macro selection failed: " + error.GetType().Name)); }
                });
                else Report(slot, new(ActionOutcome.MissingToken, "Overlay click did not match a physical gesture"));
            };
        };
        editCommands = () =>
        {
            Dismiss();
            var features = services.GetRequiredService<FeatureHost>();
            var settings = services.GetRequiredService<ISettingsStore<SettingsDocument>>();
            var mapped = BindingMapper.Map(FeatureDefaultBinding.Apply(settings.Snapshot.Hotkeys, features.DefaultBindings),
                services.GetRequiredService<HotkeyConfiguration>(), features.Commands);
            new MacroSettingsWindow(model, MacroBindingLabels.Title(mapped.Bindings)).Show();
        };
        model.Opened += Open;
        model.Closed += Close;
    }
    public void Initialize()
    {
        window = (HostedOverlayWindow)Window.GetWindow(this);
        host.SetVisible(window, false);
        window.IsVisibleChanged += VisibilityChanged;
        window.PhysicalPlacement = (client, scale) => PopupPlacement.Place(client,
            styler.GetWorkArea(client), origin.X, origin.Y, 400, 300, scale, centered: true);
        window.Closed += (_, _) =>
        {
            window.PhysicalPlacement = null;
            model.Opened -= Open; model.Closed -= Close;
            passive.Close(this);
        };
    }
    private void Guard(Action action) => OverlayFailureGuard.Run(action,
        () => { passive.Close(this); if (window is not null) host.SetVisible(window, false); model.Reset(); },
        error => logger.LogWarning("Macro wheel failed: {ExceptionType}", error.GetType().Name));
    private void Open(long id, IReadOnlyList<string> commands, WheelOrigin captured) => Dispatcher.BeginInvoke(() => Guard(() =>
    {
        if (window is null || !model.IsCurrent(id)) return;
        var state = game.Current;
        if (!state.Found || !state.IsForeground || state.IsMinimized) { model.Reset(); return; }
        presentedVersion = id;
        origin = captured;
        Children.Clear();
        var content = MacroWheelBuilder.Build(new(commands), CardTheme.Default);
        Children.Add(content);
        slots = OnDemandControls.Buttons(content).Where(b => b.Tag is int).ToArray();
        foreach (var button in slots) wireSlot(button, (int)button.Tag);
        OnDemandControls.Buttons(content).Single(b => b.Tag is string).Click += (_, _) => editCommands();
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
        if (input.Message == 0x200)
        {
            var point = PointFromScreen(new Point(input.X, input.Y));
            var selected = MacroWheelBuilder.Direction(point);
            foreach (var button in slots)
                MacroWheelBuilder.Highlight(button, (int)button.Tag == selected, CardTheme.Default);
        }
        var origin = PointToScreen(new Point());
        var end = PointToScreen(new Point(ActualWidth, ActualHeight));
        var inside = input.X >= origin.X && input.X < end.X && input.Y >= origin.Y && input.Y < end.Y;
        if (input.Message == 0x100 || (!inside && input.Message is 0x201 or 0x204 or 0x207 or 0x20b))
            Guard(Dismiss);
    }
}
