using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExileSight.Features.MapTracker;
using Microsoft.Win32;
namespace ExileSight.App.Features.MapTracker;

public sealed class TrackerWindow : Window
{
    private readonly TrackerModel model;
    private readonly ComboBox league = new() { Width = 170, IsEditable = true };
    private readonly TextBox map = new() { Width = 120, ToolTip = "Map name filter" };
    private readonly TextBox tier = new() { Width = 45, ToolTip = "Tier filter" };
    private readonly DatePicker from = new() { Width = 125, ToolTip = "From date" };
    private readonly DatePicker to = new() { Width = 125, ToolTip = "To date" };
    private readonly DataGrid grid = new() { IsReadOnly = true, AutoGenerateColumns = false };
    private readonly TextBox notes = new() { MaxLength = 4096, MinWidth = 200, ToolTip = "Notes for selected run" };
    private readonly TextBlock summary = new() { Margin = new(8), TextWrapping = TextWrapping.Wrap };
    private MapRun[] rows = [];
    public TrackerWindow(TrackerModel model)
    {
        this.model = model; Title = "Map tracker — Ctrl+Shift+K: /kills"; Width = 1100; Height = 650;
        league.ItemsSource = model.Leagues; league.Text = model.League;
        var root = new DockPanel { Margin = new(10) }; Content = root;
        var filters = new WrapPanel(); DockPanel.SetDock(filters, Dock.Top); root.Children.Add(filters);
        foreach (var control in new FrameworkElement[] { new TextBlock { Text = "League " }, league,
            new TextBlock { Text = " Map " }, map, new TextBlock { Text = " Tier " }, tier, from, to }) filters.Children.Add(control);
        Button(filters, "Record in league", () => { model.SelectLeague(league.Text.Trim()); Refresh(); });
        Button(filters, "Apply / Refresh", Refresh);
        Button(filters, "Export filtered CSV", Export);
        var identities = new WrapPanel();
        DockPanel.SetDock(identities, Dock.Top); root.Children.Add(identities);
        string[]? displayedCandidates = null;
        void RefreshCandidates()
        {
            var candidates = model.Candidates;
            if (displayedCandidates?.SequenceEqual(candidates) == true) return;
            displayedCandidates = candidates; identities.Children.Clear();
            identities.Children.Add(new TextBlock { Text = candidates.Length == 0
                ? "Character candidates appear after a level-up log event." : "Confirm your character from the log: " });
            foreach (var candidate in candidates)
                Button(identities, candidate, () => model.ConfirmCharacter(candidate));
        }
        RefreshCandidates();
        var storageStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(8) };
        DockPanel.SetDock(storageStatus, Dock.Bottom); root.Children.Add(storageStatus);
        // Local status only: this timer never sends input or reads/writes history.
        var statusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        statusTimer.Tick += (_, _) => { storageStatus.Text = model.Status; RefreshCandidates(); };
        Closed += (_, _) => statusTimer.Stop();
        storageStatus.Text = model.Status; statusTimer.Start();
        DockPanel.SetDock(summary, Dock.Bottom); root.Children.Add(summary);
        var edit = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(edit, Dock.Bottom);
        edit.Children.Add(notes); Button(edit, "Save notes", () =>
        { if (grid.SelectedItem is MapRun run) { model.Notes(run, notes.Text); Refresh(); } });
        root.Children.Add(edit); root.Children.Add(grid);
        grid.SelectionChanged += (_, _) => notes.Text = (grid.SelectedItem as MapRun)?.Notes ?? "";
        foreach (var column in new[] { "Map", "Tier", "Started", "Seconds", "Portals", "DeathsText", "Kills",
            "MechanicsText", "ModsText", "Notes", "Incomplete" })
            grid.Columns.Add(new DataGridTextColumn { Header = column.Replace("Text", ""), Binding = new System.Windows.Data.Binding(column) });
        Refresh();
    }
    private static void Button(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Margin = new(4), Padding = new(6) };
        button.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
            { MessageBox.Show("Map history unavailable: " + e.GetType().Name); }
        };
        panel.Children.Add(button);
    }
    private void Refresh()
    {
        try
        {
            rows = model.Query(league.Text, map.Text, int.TryParse(tier.Text, out var value) ? value : null,
                from.SelectedDate, to.SelectedDate);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        { rows = []; grid.ItemsSource = rows; summary.Text = "History is read-only/unavailable: " + e.GetType().Name; return; }
        grid.ItemsSource = rows;
        summary.Text = $"{rows.Length} runs · {TimeSpan.FromSeconds(rows.Sum(r => r.Seconds))} · " +
            $"{TrackerModel.DeathTotals(rows)} · {rows.Sum(r => r.Portals)} portals · " +
            $"{rows.Sum(r => r.Kills ?? 0)} sampled kills ({rows.Count(r => r.Kills is null)} unknown)";
    }
    private void Export()
    {
        var dialog = new SaveFileDialog { Filter = "CSV files|*.csv", FileName = "map-runs.csv" };
        if (dialog.ShowDialog(this) == true) File.WriteAllText(dialog.FileName, TrackerModel.Csv(rows));
    }
}
