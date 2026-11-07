using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ExileSight.App.Overlay;
using ExileSight.Core.Overlay;
using ExileSight.Features.Guide;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Win32;

namespace ExileSight.App.Features.Guide;

public sealed class GuideView
{
    private readonly GuideModel model;
    private readonly OverlayHost host;
    private readonly IGameWindow game;
    private readonly HostedOverlayWindow panel;
    private readonly TextBlock text = new() { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock status = new() { Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    public GuideView(GuideModel model, OverlayHost host, IGameWindow game)
    {
        this.model = model; this.host = host; this.game = game;
        var content = new StackPanel { Background = Brushes.Black, Margin = new Thickness(8) };
        content.Children.Add(new TextBlock { Text = "Guide · Ctrl+Shift+G · ←/→ with Ctrl+Shift", Foreground = Brushes.White });
        content.Children.Add(new ScrollViewer { Height = 300, Content = text, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var buttons = new WrapPanel(); content.Children.Add(buttons); content.Children.Add(status);
        void Button(string title, Action action)
        {
            var button = new Button { Content = title, Focusable = false, Margin = new Thickness(3) };
            button.Click += (_, _) => Run(action); buttons.Children.Add(button);
        }
        Button("Back", () => model.Navigate(-1)); Button("Next", () => model.Navigate(1));
        Button("Import JSON (resets)", Import); Button("Export CSV", Export);
        Button("Reset run", () => model.Reset());
        Button("League start (resets)", () => model.Reset(changeLeague: true));
        Button("Bandit (resets)", () => model.Reset(changeBandit: true)); Button("Close", model.Hide);
        panel = host.Register(content, new OverlayRect(20, 320, 460, 440), clickThrough: false);
        host.SetVisible(panel, false);
        host.Changed += FocusChanged;
        timer.Tick += (_, _) => Refresh(); timer.Start();
        panel.Closed += (_, _) => { timer.Stop(); host.Changed -= FocusChanged; };
    }
    private void FocusChanged(object? sender, EventArgs args)
    {
        if (!game.Current.IsForeground || !game.Current.Found || game.Current.IsMinimized) model.Hide();
    }
    private void Run(Action action)
    {
        try { action(); status.Text = ""; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { status.Text = "Guide operation failed: " + e.GetType().Name; }
        Refresh();
    }
    private void Refresh()
    {
        try
        {
            var state = model.Snapshot(); text.Text = state.Text;
            host.SetVisible(panel, state.Visible && game.Current.IsForeground && game.Current.Found && !game.Current.IsMinimized);
        }
        catch (Exception)
        { model.Hide(); try { host.SetVisible(panel, false); } catch (Exception) { panel.Hide(); } }
    }
    private void Import()
    {
        var dialog = new OpenFileDialog { Filter = "Guide JSON|*.json", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        using var file = File.OpenRead(dialog.FileName);
        if (file.Length > GuideImport.MaxBytes) throw new ArgumentException("Guide exceeds size limit.");
        var bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes); model.Import(bytes);
    }
    private void Export()
    {
        var csv = model.Export();
        var dialog = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "guide-timers.csv", AddExtension = true };
        if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, csv);
    }
}
