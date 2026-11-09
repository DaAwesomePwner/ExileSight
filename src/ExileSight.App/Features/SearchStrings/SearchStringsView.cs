using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.App.Features.Macros;
using System.Windows.Interop;
using ExileSight.Core.Features;
using ExileSight.Platform.Windows.Input;
using ExileSight.App.Overlay;
using ExileSight.Core.Input;
using ExileSight.Features.SearchStrings;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.Features.SearchStrings;

/// <summary>Hosted NOACTIVATE menu; the hook pairs physical clicks before command execution.</summary>
public sealed class SearchStringsView : ContentControl
{
    private readonly OverlayTheme overlayTheme;
    private readonly SearchMenu model;
    private readonly FeatureHost features;
    private readonly HookHost hooks;
    private readonly OverlayHost host;
    private readonly PassiveOverlayInput passive;
    private readonly ILogger<SearchStringsView> logger;
    private FrameworkElement panel = new StackPanel();
    private readonly SearchStringsSelection selection;
    private HostedOverlayWindow? window;
    private bool vendorSelected;
    public SearchStringsView(SearchMenu model, FeatureHost features, HookHost hooks, OverlayHost host,
        PassiveOverlayInput passive, ILogger<SearchStringsView> logger, ISettingsStore<SettingsDocument> overlaySettings)
    {
        overlayTheme = new(overlaySettings);
        this.model = model; this.features = features; this.hooks = hooks; this.host = host; this.passive = passive; this.logger = logger;
        selection = new(model);
        Width = 320; HorizontalAlignment = HorizontalAlignment.Left; Content = panel;
        VerticalAlignment = VerticalAlignment.Top;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Focusable = false;
        model.Opened += Open;
    }
    public void Initialize()
    {
        window = (HostedOverlayWindow)Window.GetWindow(this);
        host.SetVisible(window, false);
        overlayTheme.Watch(window, () => { if (window.IsVisible) Guard(() => Rebuild(vendorSelected)); });
        window.IsVisibleChanged += VisibilityChanged;
        window.Closed += (_, _) => { model.Opened -= Open; passive.Close(this); model.Cancel(); };
    }
    private void Open() => Dispatcher.BeginInvoke(() => Guard(() =>
    {
        Vendors();
        host.SetVisible(window!, true);
    }));
    private void Guard(Action action) => OverlayFailureGuard.Run(action,
        () => { model.Cancel(); passive.Close(this); host.SetVisible(window!, false); },
        error => logger.LogWarning("Search menu failed: {ExceptionType}", error.GetType().Name));
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (window!.IsVisible) passive.Open(this, Observed);
        else { passive.Close(this); model.Cancel(); }
    }
    private void Vendors() => Rebuild(false);
    private SearchRowPresentation Row(int entry) => new(model.Entries[entry].Name, model.Text(entry),
        selection.Line(entry), selection.LineCount(entry));
    private void Rebuild(bool selected)
    {
        vendorSelected = selected;
        var theme = overlayTheme.Current;
        panel = SearchStringsBuilder.Build(new(model.Vendors, selected ? model.Vendor : null,
            selected ? Enumerable.Range(0, model.Entries.Count).Select(Row).ToArray() : [], model.Notice), theme);
        Content = panel;
        foreach (var button in OnDemandControls.Buttons(panel))
        {
            if (button.Name == "Close") { button.Click += (_, _) => Guard(Close); continue; }
            if (button.Name == "Back")
            {
                button.Click += (_, _) => Guard(() => { model.Cancel(); Vendors(); });
                continue;
            }
            if (button.Tag is string vendor)
            {
                button.Click += (_, _) => Guard(() =>
                {
                    selection.ChooseVendor(vendor);
                    Rebuild(true);
                });
                continue;
            }
            var entry = (int)button.Tag;
            button.PreviewMouseWheel += (_, e) => Guard(() =>
            {
                var direction = e.Delta > 0 ? -1 : 1;
                selection.Cycle(entry, direction);
                button.Content = SearchStringsBuilder.Row(Row(entry), theme);
                e.Handled = true;
            });
            button.PreviewMouseLeftButtonUp += async (_, e) =>
            {
                if (!button.IsMouseOver || !button.IsEnabled || !panel.IsEnabled) return;
                panel.IsEnabled = false;
                try
                {
                    // SearchClick wraps Prepare and redemption in OverlayFailureGuard.
                    await SearchClick.RunAsync(() => model.Prepare(entry), () =>
                    {
                        var point = button.PointToScreen(e.GetPosition(button));
                        return hooks.RedeemOverlayClick(new WindowInteropHelper(window!).Handle,
                            (int)point.X, (int)point.Y, "searchstr:paste");
                    }, token => Task.Run(() => features.Commands.Single(c => c.Command == "searchstr:paste").Execute(token)),
                        model.Cancel, Close, error => logger.LogWarning(
                            "Search selection failed: {ExceptionType}", error.GetType().Name));
                }
                finally { panel.IsEnabled = true; }
            };

        }
    }
    private void Close() { model.Cancel(); host.SetVisible(window!, false); }
    private void Observed(PassiveInput input)
    {
        if (window?.IsVisible != true) return;
        Guard(() =>
        {
            var start = window.PointToScreen(new Point());
            var end = window.PointToScreen(new Point(window.ActualWidth, window.ActualHeight));
            var inside = input.X >= start.X && input.X < end.X && input.Y >= start.Y && input.Y < end.Y;
            if (input.Message == 0x100 || (input.Message is 0x201 or 0x204 or 0x207 or 0x20b && !inside)) Close();
        });
    }
}
