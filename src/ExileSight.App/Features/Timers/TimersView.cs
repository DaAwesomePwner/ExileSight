using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using System.Windows.Threading;
using ExileSight.App.Overlay;
using ExileSight.Core.Overlay;
using ExileSight.Features.Timers;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Features.Timers;

public sealed class TimersView
{
    private readonly TimersModel model;
    private readonly OverlayHost host;
    private readonly IGameWindow game;
    private readonly HostedOverlayWindow panel, passive, toast;
    private readonly DispatcherTimer ticker = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly ListBox list = new() { Height = 155, Focusable = false, Background = CardTheme.Default.OverlayBackground, Foreground = CardTheme.Default.OverlayForeground, DisplayMemberPath = "Text" };
    private readonly TimerEditor editor = new();
    private readonly StackPanel controls = new();
    private readonly Border passiveContent = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly CardTheme theme = CardTheme.Default;
    private readonly TextBlock status = TimerEditor.Label("");
    private readonly TextBlock toastText = TimerEditor.Label("");
    private readonly TimerRows rows = new();
    private long toastUntil;
    private TimersVisibility visibility;
    private readonly Dictionary<HostedOverlayWindow, bool> requested = [];
    public TimersView(TimersModel model, OverlayHost host, IGameWindow game)
    {
        this.model = model; this.host = host; this.game = game;
        list.ItemsSource = rows.Items;
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(UIElement.FocusableProperty, false));
        list.ItemContainerStyle = itemStyle;
        list.PreviewMouseLeftButtonDown += (_, e) =>
        {
            // WPF ListBoxItem selects only if Focus() succeeds. NOACTIVATE must not
            // depend on that path: select the hit row and consume it before bubbling.
            if (!visibility.Interactive || e.OriginalSource is not DependencyObject source ||
                ItemsControl.ContainerFromElement(list, source) is not ListBoxItem item) return;
            list.SelectedItem = list.ItemContainerGenerator.ItemFromContainer(item);
            e.Handled = true;
        };
        var content = new StackPanel { Background = CardTheme.Default.OverlayBackground, Margin = new Thickness(6) };
        content.Children.Add(TimerEditor.Label("Timers · hold Tab to view · Ctrl+Shift+T to edit"));
        content.Children.Add(list); content.Children.Add(controls); content.Children.Add(status);
        controls.Children.Add(editor);
        var buttons = new WrapPanel();
        buttons.Children.Add(TimerEditor.Button("Add", () => Run(() => model.Add(editor.TimerName, editor.Kind, editor.Duration, editor.Alert))));
        buttons.Children.Add(TimerEditor.Button("Rename / alert", () => Selected(id => { model.Rename(id, editor.TimerName); model.SetAlert(id, editor.Alert); })));
        buttons.Children.Add(TimerEditor.Button("Start", () => Selected(model.Start)));
        buttons.Children.Add(TimerEditor.Button("Pause", () => Selected(model.Pause)));
        buttons.Children.Add(TimerEditor.Button("Reset", () => Selected(model.Reset)));
        buttons.Children.Add(TimerEditor.Button("Delete", () => Selected(model.Delete)));
        buttons.Children.Add(TimerEditor.Button("Close", model.LoseFocus));
        controls.Children.Add(buttons);
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is TimerRow selected && model.Snapshot().FirstOrDefault(r => r.Id == selected.Id) is { } row)
                editor.Select(row);
        };
        panel = host.Register(content, DefaultOverlayLayout.Timers, clickThrough: false);
        passive = host.Register(passiveContent,
            DefaultOverlayLayout.CompactTimers);
        toast = host.Register(new Border { Background = CardTheme.Default.OverlayBackground, Padding = new Thickness(10), Child = toastText },
            DefaultOverlayLayout.TimersToast);
        SetVisible(panel, false); SetVisible(passive, false); SetVisible(toast, false);
        // Settings visibility changes bypass model notifications.
        DependencyPropertyChangedEventHandler displayVisibilityChanged = (_, _) => ScheduleTick();
        panel.IsVisibleChanged += displayVisibilityChanged;
        passive.IsVisibleChanged += displayVisibilityChanged;
        host.Changed += VisibilityChanged;
        model.VisibilityChanged += Present;
        panel.Closed += (_, _) =>
        {
            panel.IsVisibleChanged -= displayVisibilityChanged;
            passive.IsVisibleChanged -= displayVisibilityChanged;
            ticker.Stop(); host.Changed -= VisibilityChanged; model.VisibilityChanged -= Present;
        };
        ticker.Tick += (_, _) => Tick();
        ScheduleTick();
        status.Text = model.Notice ?? "Choose a timer, or add a named countdown / stopwatch.";
    }
    private void VisibilityChanged(object? sender, EventArgs args)
    {
        if (!game.Current.IsForeground || game.Current.IsMinimized || !game.Current.Found) model.LoseFocus();
    }
    private void Present(TimersVisibility state)
    {
        if (!panel.Dispatcher.CheckAccess())
        {
            panel.Dispatcher.BeginInvoke(() => Present(state), DispatcherPriority.Normal);
            return;
        }
        // Reconcile queued input with the current game focus before exposing an interactive panel.
        if (!game.Current.IsForeground || game.Current.IsMinimized || !game.Current.Found) state = default;
        visibility = state;
        if (state.Visible) Refresh();
        controls.Visibility = state.Interactive ? Visibility.Visible : Visibility.Collapsed;
        if (!state.Interactive) list.SelectedItem = null;
        SetVisible(panel, state.Interactive);
        SetVisible(passive, state.Visible && !state.Interactive);
        ScheduleTick();
    }
    private void SetVisible(HostedOverlayWindow window, bool visible)
    {
        if (requested.TryGetValue(window, out var previous) && previous == visible) return;
        requested[window] = visible;
        host.SetVisible(window, visible);
    }
    private void Selected(Action<Guid> action)
    {
        if (list.SelectedItem is not TimerRow row) { status.Text = "Select a timer first."; return; }
        Run(() => action(row.Id));
    }
    private void Run(Action action)
    {
        try { action(); status.Text = "Saved."; if (visibility.Visible) Refresh(); ScheduleTick(); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { status.Text = "Could not save: " + error.GetType().Name; }
    }
    private void Tick()
    {
        try
        {
            var completed = model.Tick();
            if (completed.Length > 0)
            {
                toastText.Text = "Countdown ended: " + string.Join(", ", completed.Select(r => r.Name));
                toastUntil = Environment.TickCount64 + 6000;
                SetVisible(toast, true);
            }
            if (panel.IsVisible || passive.IsVisible) Refresh();
            if (Environment.TickCount64 >= toastUntil) SetVisible(toast, false);
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
        { status.Text = "Timers unavailable: " + error.GetType().Name; }
        finally { ScheduleTick(); }
    }
    // Hidden panels recheck countdown deadlines within five seconds of a wall-clock jump.
    private void ScheduleTick()
    {
        ticker.Stop();
        var delay = panel.IsVisible || passive.IsVisible ? 250d : double.PositiveInfinity;
        foreach (var row in model.Snapshot().Where(r => r.Kind == TimerKind.Countdown && !r.Alerted))
            if (row.EndUtc is not null || row.SavedValue == TimeSpan.Zero)
                delay = Math.Min(delay, model.Value(row.Id).TotalMilliseconds);
        if (toastUntil > Environment.TickCount64)
            delay = Math.Min(delay, toastUntil - Environment.TickCount64);
        if (double.IsPositiveInfinity(delay)) return;
        ticker.Interval = TimeSpan.FromMilliseconds(Math.Clamp(delay, 250, 5000));
        ticker.Start();
    }
    private void Refresh()
    {
        rows.Refresh(model);
        if (visibility.Visible && !visibility.Interactive)
            passiveContent.Child = TimersCardBuilder.Build(model.Snapshot()
                .Select(r => new TimerCardRow(r.Name, r.Kind, model.Value(r.Id), r.Duration)).ToArray(), theme);
    }
}
