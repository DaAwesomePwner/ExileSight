using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.Core.Input;
using static ExileSight.App.Shell.ShellElements;
namespace ExileSight.App.Shell.Pages;

public static class TransparencyPageBuilder
{
    public static FrameworkElement Build(ActionLogSnapshot model, CardTheme t, DateTime? today = null)
    {
        var panel = new StackPanel();
        panel.Children.Add(Text("Everything ExileSight sent to the game", t, 24, true));
        panel.Children.Add(Text("One line per logged attempt. Nothing is sent on its own. Stored only on this PC; no item text, amounts or search strings.", t, muted: true));
        var open = Button(model.Missing ? "Open log folder" : "Open actions.log", "OpenActionsLog", t);
        open.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(open);
        if (model.Note is not null) panel.Children.Add(Text(model.Note, t));
        var table = new StackPanel();
        table.Children.Add(Row(["TIME", "TRIGGER", "ACTION", "RESULT"], t, false));
        foreach (var entry in model.Entries)
            table.Children.Add(Row([FormatTime(entry.Timestamp, today ?? DateTime.Today), entry.Trigger,
                ActionName(entry.Action) + (entry.Verb is null ? "" : " " + entry.Verb), entry.Result], t, true, entry.Sent));
        if (model.Entries.Count == 0) table.Children.Add(Text("No recorded actions.", t, muted: true));
        panel.Children.Add(Card(table, t));
        panel.Children.Add(Text($"Last 200 log lines, newest first · {model.Skipped} malformed or partial lines skipped." +
            (model.Truncated ? " Showing a bounded tail of the file." : ""), t, 12, muted: true));
        panel.Children.Add(Text("Reads Client.txt and item text after its own copy. Contacts poe.ninja and GitHub only. Never changes the game window or game files, and never reads or writes game memory.", t));
        return panel;
    }
    public static string FormatTime(DateTimeOffset timestamp, DateTime today)
    {
        var local = timestamp.ToLocalTime();
        return local.ToString(local.Date == today.Date ? "HH:mm:ss" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
    public static string ActionName(string action) => action switch
    {
        "CopyItem" => "Copy item", "PasteSearch" => "Paste search", "SetListingAmount" => "Set listing amount",
        "SetPriceNote" => "Set price note", "SendChatCommand" => "Chat command", _ => action
    };
    private static Grid Row(string[] cells, CardTheme t, bool data, bool sent = false)
    {
        var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        foreach (var width in new[] { 1.3, 1.1, 1.4, 1.0 }) row.ColumnDefinitions.Add(new() { Width = new GridLength(width, GridUnitType.Star) });
        for (var i = 0; i < cells.Length; i++)
        {
            var text = Text(cells[i], t, data ? 14 : 12, !data, !data);
            text.Margin = new Thickness(0, 0, 12, 0);
            if (i == 3 && data) text.Foreground = sent ? t.OverlayAccent : t.TooltipLow;
            Grid.SetColumn(text, i); row.Children.Add(text);
        }
        return row;
    }
}
