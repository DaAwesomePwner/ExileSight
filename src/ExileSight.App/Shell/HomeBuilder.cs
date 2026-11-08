using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.Core.Status;
using static ExileSight.App.Shell.ShellElements;
namespace ExileSight.App.Shell;

public static class HomeBuilder
{
    public static FrameworkElement Build(ShellModel model, CardTheme t, bool narrow)
    {
        var body = new StackPanel();
        body.Children.Add(Text(HomeStatus.Title(model.Status), t, 24, true));
        body.Children.Add(Text("Your game, data and recent activity.", t, muted: true));
        var leagueRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 16) };
        leagueRow.Children.Add(Text("League  ", t, muted: true));
        var league = new ComboBox { IsEditable = true, Text = model.League, Width = 200, Tag = "League" };
        league.Items.Add("Standard"); league.Items.Add("Hardcore");
        if (!string.IsNullOrEmpty(model.League) && model.League is not ("Standard" or "Hardcore")) league.Items.Add(model.League);
        leagueRow.Children.Add(league); leagueRow.Children.Add(Button("Save league", "SaveLeague", t)); body.Children.Add(leagueRow);
        var tiles = new WrapPanel();
        foreach (var status in model.Status)
        {
            var content = new StackPanel();
            var label = Text("●  " + status.Label, t, 12);
            label.Foreground = status.Ok ? t.OverlayAccent : t.TooltipLow;
            content.Children.Add(label); content.Children.Add(Text(status.Summary, t, 14, true));
            content.Children.Add(Text(status.Detail, t, 12, muted: true));
            var tile = Card(content, t); tile.Width = narrow ? 230 : 220; tiles.Children.Add(tile);
        }
        body.Children.Add(tiles);
        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        if (!narrow) columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.RowDefinitions.Add(new RowDefinition()); columns.RowDefinitions.Add(new RowDefinition());
        var keys = new StackPanel(); keys.Children.Add(Text("Your keys", t, 18, true));
        keys.Children.Add(Keys(model.Keys, t)); keys.Children.Add(Button("View hotkeys", "Hotkeys", t));
        columns.Children.Add(Card(keys, t));
        var session = Card(Session(model.Session, t), t);
        if (narrow) Grid.SetRow(session, 1); else Grid.SetColumn(session, 1);
        columns.Children.Add(session); body.Children.Add(columns);
        return body;
    }
    private static FrameworkElement Session(LastSession? session, CardTheme t)
    {
        var body = new StackPanel(); body.Children.Add(Text("Last session", t, 18, true));
        if (session is null) body.Children.Add(Text("No recorded maps yet. Choose a league in Map log to begin.", t, muted: true));
        else
        {
            body.Children.Add(Text(session.Started.ToString("g", CultureInfo.InvariantCulture), t, 12, muted: true));
            body.Children.Add(Text($"{session.Maps.Count} maps run", t, 24, true));
            body.Children.Add(Text(TimeSpan.FromSeconds(session.AverageSeconds).ToString(@"hh\:mm\:ss") + " average map time", t));
            body.Children.Add(Text(session.DeathsKnown ? $"{session.KnownDeaths} deaths" : $"Deaths unknown ({session.KnownDeaths} known)", t));
            body.Children.Add(Text(session.DivineSold.ToString("0.##", CultureInfo.InvariantCulture) + " divine marked sold", t));
            var bars = new StackPanel { Orientation = Orientation.Horizontal, Height = 64 };
            var max = Math.Max(1, session.Maps.Max(m => m.Seconds));
            foreach (var map in session.Maps)
                bars.Children.Add(new Border { Width = 14, Height = Math.Max(2, map.Seconds / max * 56),
                    Background = map.Deaths > 0 ? t.TooltipLow : t.OverlayAccent, Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Bottom, ToolTip = $"{map.Seconds:0} seconds · deaths: {map.Deaths?.ToString() ?? "unknown"}" });
            body.Children.Add(new ScrollViewer { Content = bars, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
            body.Children.Add(Text("Map time · orange: a death in that map", t, 12, muted: true));
        }
        var actions = new WrapPanel(); actions.Children.Add(Button("Open map log", "Map log", t));
        actions.Children.Add(Button("Open sales", "Sales", t)); body.Children.Add(actions); return body;
    }
}
