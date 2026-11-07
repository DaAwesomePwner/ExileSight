using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using ExileSight.App.Overlay;
using ExileSight.App.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Core.Features;
using ExileSight.Core.Input;
using ExileSight.Core.Overlay;
using ExileSight.Platform.Windows.Input;

namespace ExileSight.App.Features;

/// <summary>App-owned view factories and button wiring; feature assemblies never reference WPF.</summary>
public sealed class FeatureViews(FeatureHost features, OverlayHost overlays, HookHost hooks, InputDiagnostics diagnostics)
{
    private readonly Dictionary<Type, Func<object, FrameworkElement>> factories = [];
    private readonly List<HostedOverlayWindow> windows = [];
    private readonly Dictionary<HostedOverlayWindow, string> owners = [];
    private string? creatingFeature;
    public void Register<T>(Func<T, FrameworkElement> factory) where T : class =>
        factories.Add(typeof(T), model => factory((T)model));
    public void Start()
    {
        foreach (var (featureId, model) in features.Overlays)
        {
            if (model is ExileSight.Features.Omni.OmniMenu) continue;
            if (!factories.TryGetValue(model.GetType(), out var factory))
                throw new InvalidOperationException($"No view registered for {model.GetType().Name}");
            FrameworkElement view;
            creatingFeature = featureId;
            try { view = factory(model); }
            finally { creatingFeature = null; }
            view.DataContext = model;
            var window = overlays.Register(view, new OverlayRect(20, 20, 400, 300), clickThrough: false);
            windows.Add(window);
            owners.Add(window, featureId);
            window.Activated += (_, _) => Snapshot();
            window.Deactivated += (_, _) => Snapshot();
            window.LocationChanged += (_, _) => Snapshot();
            window.SizeChanged += (_, _) => Snapshot();
            window.IsVisibleChanged += (_, _) => Snapshot();
        }
        overlays.Changed += (_, _) => Snapshot();
    }
    public void Bind(ButtonBase button, string commandId)
    {
        var command = features.Commands.Single(c => c.Command == commandId);
        var owner = creatingFeature;
        if (owner is null && Window.GetWindow(button) is HostedOverlayWindow existing) owners.TryGetValue(existing, out owner);
        if (owner is null || owner != command.FeatureId)
            throw new ArgumentException("Command must belong to the feature owning this overlay", nameof(commandId));
        // Mouse-up only: keyboard activation or programmatic Click does not create input authority.
        button.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left || !button.IsMouseOver || !button.IsEnabled) return;
            var window = Window.GetWindow(button);
            if (window is not HostedOverlayWindow owned || !owners.TryGetValue(owned, out var featureId) || featureId != command.FeatureId) return;
            var point = button.PointToScreen(e.GetPosition(button));
            var token = hooks.RedeemOverlayClick(new WindowInteropHelper(window).Handle, (int)point.X, (int)point.Y, commandId);
            if (token is not null) _ = ExecuteAndReportAsync(command, token);
            else Report(command, new(ActionOutcome.MissingToken, "Overlay click did not match a physical gesture"));
        };
    }
    private async Task ExecuteAndReportAsync(IFeatureCommandHandler command, IntentToken token)
    {
        ActionResult result;
        try { result = await Task.Run(() => command.Execute(token)).ConfigureAwait(false); }
        catch (Exception error) { result = new(ActionOutcome.Failed, $"Feature command failed: {error.GetType().Name}"); }
        Report(command, result);
    }
    private void Report(IFeatureCommandHandler command, ActionResult result) => diagnostics.Report(
        new HotkeyBinding(command.Command, 0, KeyModifiers.None, false, null,
            FeatureCommand: command.Command, FeatureId: command.FeatureId, CommandAction: command.RequiredAction), result);
    private void Snapshot()
    {
        var snapshot = new List<OverlayClickTarget>();
        foreach (var window in windows.Where(w => w.IsVisible))
        {
            var start = window.PointToScreen(new Point());
            var end = window.PointToScreen(new Point(window.ActualWidth, window.ActualHeight));
            snapshot.Add(new(new WindowInteropHelper(window).Handle, (int)start.X, (int)start.Y, (int)end.X, (int)end.Y));
        }
        hooks.OverlayClicks.SetTargets(OverlayZOrder.Snapshot(snapshot));
    }
}
