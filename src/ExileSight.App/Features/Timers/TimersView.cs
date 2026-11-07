using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private readonly ListBox list = new() { Height = 155, Focusable = false, Background = Brushes.Black, Foreground = Brushes.White, DisplayMemberPath = "Text" };
    private readonly TimerEditor editor = new();
    private readonly StackPanel controls = new();
    private readonly TextBlock passiveText = TimerEditor.Label("");
    private readonly TextBlock status = TimerEditor.Label("");
    private readonly TextBlock toastText = TimerEditor.Label("");
    private readonly Dictionary<Guid, TimerRow> rows = [];
    private long toastUntil;
    private TimersVisibility visibility;
    private readonly Dictionary<HostedOverlayWindow, bool> requested = [];
    private sealed class TimerRow(Guid id) : System.ComponentModel.INotifyPropertyChanged
    {
        public Guid Id { get; } = id;
        private string text = "";
        public string Text { get => text; set { if (text == value) return; text = value; PropertyChanged?.Invoke(this, new(nameof(Text))); } }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
    public TimersView(TimersModel model, OverlayHost host, IGameWindow game)
    {
        this.model = model; this.host = host; this.game = game;
        var content = new StackPanel { Background = Brushes.Black, Margin = new Thickness(6) };
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
        panel = host.Register(content, new OverlayRect(20, 80, 400, 530), clickThrough: false);
        passive = host.Register(new Border { Background = Brushes.Black, Padding = new Thickness(8), Child = passiveText },
            new OverlayRect(20, 80, 400, 220));
        toast = host.Register(new Border { Background = Brushes.Black, Padding = new Thickness(10), Child = toastText },
            new OverlayRect(440, 80, 300, 100));
        SetVisible(panel, false); SetVisible(passive, false); SetVisible(toast, false);
        host.Changed += VisibilityChanged;
        model.VisibilityChanged += Present;
        panel.Closed += (_, _) => { ticker.Stop(); host.Changed -= VisibilityChanged; model.VisibilityChanged -= Present; };
        ticker.Tick += (_, _) => Tick(); ticker.Start();
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
        try { action(); status.Text = "Saved."; if (visibility.Visible) Refresh(); }
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
            if (visibility.Visible) Refresh();
            if (Environment.TickCount64 >= toastUntil) SetVisible(toast, false);
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
        { status.Text = "Timers unavailable: " + error.GetType().Name; }
    }
    private void Refresh()
    {
        var snapshot = model.Snapshot();
        foreach (var old in rows.Keys.Except(snapshot.Select(r => r.Id)).ToArray())
        { list.Items.Remove(rows[old]); rows.Remove(old); }
        foreach (var state in snapshot)
        {
            if (!rows.TryGetValue(state.Id, out var row))
            { row = new(state.Id); rows.Add(state.Id, row); list.Items.Add(row); }
            var value = model.Value(state.Id);
            row.Text = $"{state.Name} · {(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}" +
                (state.Alerted ? " · ended" : state.IsRunning ? " · running" : " · paused");
        }
        var text = "Timers · Ctrl+Shift+T to edit\n" + string.Join("\n", snapshot.Take(8).Select(r => rows[r.Id].Text)) +
            (snapshot.Length > 8 ? $"\n+{snapshot.Length - 8} more · toggle to see all" : "");
        if (passiveText.Text != text) passiveText.Text = text;
    }
}
