using System.Windows;
using System.Windows.Threading;
using ExileSight.Core.Overlay;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Overlay;

/// <summary>Register windows before Start, outside input callbacks; toggle pre-created windows afterward.</summary>
public sealed class OverlayHost(IGameWindow game, IOverlayWindowStyler styler,
    ISettingsStore<SettingsDocument>? settings = null) : IDisposable
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly List<HostedOverlayWindow> windows = [];
    private bool started;
    private bool disposed;
    private bool enabled = true;
    private double? scaleOverride;
    public event EventHandler? Changed;
    public double EffectiveScale => OverlayRules.Scale(game.Current.ClientRect.Height, scaleOverride);
    public bool IsVisible => windows.Any(window => window.IsVisible);
    public string? LastError { get; private set; }

    public HostedOverlayWindow Register(FrameworkElement content, OverlayRect design, bool clickThrough = true)
    {
        dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) throw new InvalidOperationException("Register overlays before starting the host, never from input callbacks.");
        if (!double.IsFinite(design.X) || !double.IsFinite(design.Y) || !double.IsFinite(design.Width)
            || !double.IsFinite(design.Height) || design.Width <= 0 || design.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(design));
        var window = new HostedOverlayWindow(styler, content, design, clickThrough);
        window.RefreshRequested += Update;
        windows.Add(window);
        return window;
    }

    public void ApplySettings(OverlaySettings settings)
    {
        dispatcher.VerifyAccess();
        enabled = settings.Enabled;
        scaleOverride = settings.ScaleOverride;
        Update(this, EventArgs.Empty);
    }

    public void SetVisible(HostedOverlayWindow window, bool visible)
    {
        dispatcher.VerifyAccess();
        if (!windows.Contains(window)) throw new ArgumentException("Window is not registered with this host.", nameof(window));
        window.RequestedVisible = visible;
        Update(this, EventArgs.Empty);
    }

    public void Start()
    {
        dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) return;
        started = true;
        if (settings is not null)
        {
            ApplySettings(settings.Snapshot.Overlay);
            settings.Changed += SettingsChanged;
        }
        game.Changed += Update;
        game.Start();
        Update(this, EventArgs.Empty);
    }

    private void SettingsChanged(object? sender, SettingsChangedEventArgs args) => dispatcher.BeginInvoke(() =>
    {
        if (!disposed && settings is not null) ApplySettings(settings.Snapshot.Overlay);
    });

    private void Update(object? sender, EventArgs args)
    {
        dispatcher.VerifyAccess();
        if (disposed) return;
        LastError = null;
        foreach (var window in windows)
        {
            try { window.Update(game.Current, enabled, scaleOverride); }
            catch (Exception error) { window.Hide(); LastError = error.Message; }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        dispatcher.VerifyAccess();
        if (disposed) return;
        disposed = true;
        if (settings is not null) settings.Changed -= SettingsChanged;
        game.Changed -= Update;
        game.Dispose();
        foreach (var window in windows)
        {
            window.RefreshRequested -= Update;
            window.Close();
        }
        windows.Clear();
    }
}
