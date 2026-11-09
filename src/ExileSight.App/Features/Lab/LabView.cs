using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ExileSight.App.Overlay;
using ExileSight.Core.Overlay;
using ExileSight.Features.Lab;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Shell;
using Microsoft.Win32;

namespace ExileSight.App.Features.Lab;

public sealed class LabView
{
    private readonly LabModel model;
    private readonly OverlayHost host;
    private readonly HostedOverlayWindow panel;
    private readonly Canvas map = new() { Width = 430, Height = 265 };
    private readonly TextBlock details = Text();
    private readonly TextBlock error = Text();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ComboBox difficulty = new() { ItemsSource = LabImport.Difficulties, SelectedIndex = 0, Width = 100 };
    private bool selecting;
    private (LabLayout? Layout, string? RoomId, string Tracking)? drawnState;
    public LabView(LabModel model, OverlayHost host, IBrowser browser, IClipboard clipboard)
    {
        this.model = model; this.host = host;
        var content = new StackPanel { Background = Brushes.Black, Margin = new Thickness(8) };
        content.Children.Add(Text("Lab · Ctrl+Shift+L · Import the daily compass JSON"));
        var buttons = new WrapPanel(); content.Children.Add(buttons); buttons.Children.Add(difficulty);
        void Button(string title, Action action)
        {
            var button = new Button { Content = title, Focusable = false, Margin = new Thickness(2) };
            button.Click += (_, _) => Run(action); buttons.Children.Add(button);
        }
        Button("Open poelab", () => browser.Open("https://www.poelab.com/", new LabFeature().Manifest.BrowserHosts));
        Button("Import file…", () =>
        {
            var dialog = new OpenFileDialog { Filter = "Lab compass JSON|*.json", CheckFileExists = true };
            if (dialog.ShowDialog() == true) ImportFile(dialog.FileName);
        });
        Button("Paste JSON", () =>
        {
            var text = clipboard.GetText() ?? "";
            if (Encoding.UTF8.GetByteCount(text) > LabImport.MaxBytes) throw new ArgumentException("Lab JSON exceeds the 1 MB limit.");
            model.Import(Encoding.UTF8.GetBytes(text));
        });
        Button("Close", model.Hide);
        content.Children.Add(error); content.Children.Add(details); content.Children.Add(map);
        content.Children.Add(Text("◆ darkshrine · K golden key · D golden door · A Argus · ⚠ traps/gauntlet"));
        var info = Text();
        try { info.Text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "lab", "info.txt")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { info.Text = "Lab tips file unavailable."; }
        content.Children.Add(new ScrollViewer { Height = 110, Content = info, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        difficulty.SelectionChanged += (_, _) => { if (!selecting) Run(() => model.Select((string)difficulty.SelectedItem)); };
        panel = host.Register(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            DefaultOverlayLayout.Lab, clickThrough: false);
        host.SetVisible(panel, false);
        model.Changed += Refresh;
        timer.Tick += (_, _) => Refresh();
        panel.IsVisibleChanged += (_, _) => timer.IsEnabled = panel.IsVisible;
        panel.Closed += (_, _) => { timer.Stop(); model.Changed -= Refresh; };
        Run(model.RestoreLatest);
    }
    private static TextBlock Text(string text = "") => new() { Text = text, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
    private void ImportFile(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose a .json file.");
        model.Import(LabStore.ReadFile(path));
    }
    private void Run(Action action)
    {
        try { action(); error.Text = ""; }
        catch (ArgumentException e) { error.Text = e.Message; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or
            System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException)
        { error.Text = "Lab operation failed: " + e.GetType().Name; }
        Refresh();
    }
    private void Refresh()
    {
        if (!panel.Dispatcher.CheckAccess()) { panel.Dispatcher.BeginInvoke(Refresh); return; }
        var state = model.Snapshot();
        selecting = true; difficulty.SelectedItem = state.Difficulty; selecting = false;
        var mapState = (state.Layout, state.RoomId, state.Tracking);
        if (drawnState != mapState)
        {
            if (state.Layout is { } mapLayout) LabMap.Draw(map, mapLayout, state.RoomId);
            else map.Children.Clear();
            drawnState = mapState;
        }
        details.Text = state.Tracking + "\nNo saved layout for this difficulty. Import a compass JSON.";
        if (state.Layout is { } layout)
        {
            details.Text = $"{layout.Difficulty} · {layout.Date:yyyy-MM-dd}" +
                (layout.IsOutdated(DateTimeOffset.UtcNow) ? " · OUTDATED (reset 00:00 UTC)" : "") +
                $"\n{state.Tracking}\nWeapon: {layout.Weapon} · Phases: {layout.Phase1}, {layout.Phase2}\nTraps: {layout.Trap1}, {layout.Trap2}";
        }
        // OverlayHost owns foreground gating, preserving requested visibility during imports/browser visits.
        host.SetVisible(panel, model.Visible);
    }
}
