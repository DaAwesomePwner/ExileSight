using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using ExileSight.App.Overlay;
using ExileSight.Core.Features;
using ExileSight.Core.Input;
using ExileSight.Core.Overlay;
using ExileSight.Platform.Windows.Input;

namespace ExileSight.App.Features;

/// <summary>App-owned view factories and button wiring; feature assemblies never reference WPF.</summary>
public sealed class FeatureViews(FeatureHost features, OverlayHost overlays, HookHost hooks)
{
    private readonly Dictionary<Type, Func<object, FrameworkElement>> factories = [];
    private readonly List<HostedOverlayWindow> windows = [];
    public void Register<T>(Func<T, FrameworkElement> factory) where T : class =>
        factories.Add(typeof(T), model => factory((T)model));
    public void Start()
    {
        foreach (var (_, model) in features.Overlays)
        {
            if (!factories.TryGetValue(model.GetType(), out var factory))
                throw new InvalidOperationException($"No view registered for {model.GetType().Name}");
            var view = factory(model);
            view.DataContext = model;
            var window = overlays.Register(view, new OverlayRect(20, 20, 400, 300), clickThrough: false);
            windows.Add(window);
            window.LocationChanged += (_, _) => Snapshot();
            window.SizeChanged += (_, _) => Snapshot();
            window.IsVisibleChanged += (_, _) => Snapshot();
        }
        overlays.Changed += (_, _) => Snapshot();
    }
    public void Bind(ButtonBase button, string commandId)
    {
        var command = features.Commands.Single(c => c.Command == commandId);
        // Mouse-up only: keyboard activation or programmatic Click does not create input authority.
        button.PreviewMouseLeftButtonUp += async (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left || !button.IsMouseOver || !button.IsEnabled) return;
            var window = Window.GetWindow(button);
            if (window is not HostedOverlayWindow owned || !windows.Contains(owned)) return;
            var point = button.PointToScreen(e.GetPosition(button));
            var token = hooks.RedeemOverlayClick(new WindowInteropHelper(window).Handle, (int)point.X, (int)point.Y, commandId);
            if (token is not null) await Task.Run(() => command.Execute(token));
        };
    }
    private void Snapshot()
    {
        var snapshot = new List<OverlayClickTarget>();
        foreach (var window in windows.Where(w => w.IsVisible))
        {
            var start = window.PointToScreen(new Point());
            var end = window.PointToScreen(new Point(window.ActualWidth, window.ActualHeight));
            snapshot.Add(new(new WindowInteropHelper(window).Handle, (int)start.X, (int)start.Y, (int)end.X, (int)end.Y));
        }
        hooks.OverlayClicks.SetTargets(snapshot);
    }
}
