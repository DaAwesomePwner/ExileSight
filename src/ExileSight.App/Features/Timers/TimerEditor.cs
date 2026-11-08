using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.Features.Timers;

namespace ExileSight.App.Features.Timers;

/// <summary>Mouse-only editor: our NOACTIVATE panel never captures keyboard focus from the game.</summary>
public sealed class TimerEditor : StackPanel
{
    private readonly TextBlock name = Label("Timer");
    private readonly TextBlock duration = Label("60 seconds");
    private readonly CheckBox alert = new() { Content = "Alert toast", IsChecked = true, Focusable = false, Foreground = CardTheme.Default.OverlayForeground };
    private TimerKind selectedKind;
    private int seconds = 60;
    public string TimerName => name.Text;
    public TimerKind Kind => selectedKind;
    public TimeSpan Duration => TimeSpan.FromSeconds(seconds);
    public bool Alert => alert.IsChecked == true;
    public TimerEditor()
    {
        Children.Add(Label("Name (mouse keyboard; game keeps focus)"));
        Children.Add(name);
        var keys = new StackPanel();
        foreach (var letters in new[] { "ABCDEFGHIJKLM", "NOPQRSTUVWXYZ", "0123456789 -" })
        {
            var row = new WrapPanel();
            foreach (var c in letters)
                row.Children.Add(Button(c == ' ' ? "␣" : c.ToString(), () => { if (name.Text.Length < 80) name.Text += c; }));
            keys.Children.Add(row);
        }
        var edit = new WrapPanel();
        edit.Children.Add(Button("Backspace", () => { if (name.Text.Length > 0) name.Text = name.Text[..^1]; }));
        edit.Children.Add(Button("Clear", () => name.Text = "")); keys.Children.Add(edit);
        Children.Add(new Expander { Header = "Edit name", Content = keys, Focusable = false, Foreground = CardTheme.Default.OverlayForeground });
        var kind = Button("Countdown", () => { });
        kind.Click += (_, _) =>
        {
            selectedKind = selectedKind == TimerKind.Countdown ? TimerKind.Stopwatch : TimerKind.Countdown;
            kind.Content = selectedKind.ToString();
        };
        var options = new WrapPanel(); options.Children.Add(kind); options.Children.Add(alert); Children.Add(options);
        Children.Add(duration);
        var time = new WrapPanel();
        foreach (var delta in new[] { -60, -10, -1, 1, 10, 60, 3600 })
            time.Children.Add(Button(delta.ToString("+0;-0", CultureInfo.InvariantCulture) + "s", () =>
            { seconds = Math.Clamp(seconds + delta, 1, 86400 * 365); duration.Text = seconds + " seconds (new countdown)"; }));
        Children.Add(time);
    }
    public void Select(TimerState row) { name.Text = row.Name; alert.IsChecked = row.Alert; }
    public static Button Button(string label, Action action)
    {
        var button = new Button { Content = label, Focusable = false, Background = CardTheme.Default.OverlayRaised, Foreground = CardTheme.Default.OverlayForeground, Padding = new Thickness(3, 1, 3, 1), Margin = new Thickness(1) };
        button.Click += (_, _) => action(); return button;
    }
    public static TextBlock Label(string text) => new() { Text = text, Foreground = CardTheme.Default.OverlayForeground, TextWrapping = TextWrapping.Wrap };
}
