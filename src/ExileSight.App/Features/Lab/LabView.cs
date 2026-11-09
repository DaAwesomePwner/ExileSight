using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.App.Features.Macros;
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
    private readonly Border content = new();
    private readonly ScrollViewer viewport = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<string, Action> actions = [];
    private string error = "";
    private readonly string tips;
    private object? drawnState;
    public LabView(LabModel model, OverlayHost host, IBrowser browser, IClipboard clipboard)
    {
        this.model = model; this.host = host;
        actions["Open poelab"] = () => browser.Open("https://www.poelab.com/", new LabFeature().Manifest.BrowserHosts);
        actions["Import file…"] = () =>
        {
            var dialog = new OpenFileDialog { Filter = "Lab compass JSON|*.json", CheckFileExists = true };
            if (dialog.ShowDialog() == true) ImportFile(dialog.FileName);
        };
        actions["Paste JSON"] = () =>
        {
            var text = clipboard.GetText() ?? "";
            if (Encoding.UTF8.GetByteCount(text) > LabImport.MaxBytes) throw new ArgumentException("Lab JSON exceeds the 1 MB limit.");
            model.Import(Encoding.UTF8.GetBytes(text));
        };
        actions["Close"] = model.Hide;
        try { tips = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "lab", "info.txt")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { tips = "Lab tips file unavailable."; }
        viewport.Content = content;
        panel = host.Register(viewport, DefaultOverlayLayout.Lab, clickThrough: false);
        host.SetVisible(panel, false);
        model.Changed += Refresh;
        timer.Tick += (_, _) => Refresh();
        panel.IsVisibleChanged += (_, _) => timer.IsEnabled = panel.IsVisible;
        panel.Closed += (_, _) => { timer.Stop(); model.Changed -= Refresh; };
        Run(model.RestoreLatest);
    }
    private void ImportFile(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose a .json file.");
        model.Import(LabStore.ReadFile(path));
    }
    private void Run(Action action)
    {
        try { action(); error = ""; }
        catch (ArgumentException e) { error = e.Message; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or
            System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException)
        { error = "Lab operation failed: " + e.GetType().Name; }
        Refresh();
    }
    private void Refresh()
    {
        if (!panel.Dispatcher.CheckAccess()) { panel.Dispatcher.BeginInvoke(Refresh); return; }
        var state = model.Snapshot();
        var now = DateTimeOffset.UtcNow;
        var next = (state, error, state.Layout?.IsOutdated(now));
        if (!Equals(drawnState, next))
        {
            var card = LabCardBuilder.Build(state, error, tips, now, CardTheme.Default);
            viewport.Resources = card.Resources;
            content.Child = card;
            foreach (var button in OnDemandControls.Buttons(card))
                button.Click += (_, _) => Run(actions[(string)button.Tag]);
            var difficulty = (ComboBox)card.FindName("Difficulty");
            difficulty.SelectionChanged += (_, _) =>
            {
                if (difficulty.SelectedItem is string selected) Run(() => model.Select(selected));
            };
            drawnState = next;
        }
        // OverlayHost owns foreground gating, preserving requested visibility during imports/browser visits.
        host.SetVisible(panel, model.Visible);
    }
}
