using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.App.Features.ItemInfo;
using ExileSight.Features.ItemInfo;
using static ExileSight.App.Shell.ShellElements;
namespace ExileSight.App.Shell.Pages;

public static class ItemRulesPageBuilder
{
    public static FrameworkElement Build(ItemRulesPageModel model, CardTheme t)
    {
        var root = new StackPanel();
        root.Children.Add(Text("Item rules", t, 24, true));
        root.Children.Add(Text("Mark the mods you want and the ones you avoid. Save changes to apply them.", t, muted: true));
        var tabs = new WrapPanel();
        for (var i = 0; i < 5; i++)
        {
            var name = i < model.Settings.Profiles.Length ? model.Settings.Profiles[i].Name : $"Profile {i + 1}";
            var tab = Button(name + (i == model.Settings.ActiveProfile ? " · active" : ""), $"Profile{i}", t);
            if (i == model.Settings.ActiveProfile) tab.Background = t.RollTrack;
            tabs.Children.Add(tab);
        }
        root.Children.Add(tabs);
        var columns = new WrapPanel();
        var editor = new StackPanel { Width = 540 };
        editor.Children.Add(Text("Add a mod · search the stats translations", t, muted: true));
        editor.Children.Add(Field("RuleSearch", "", t));
        editor.Children.Add(new ListBox { Name = "RuleResults", Height = 100, Background = t.AppNav,
            Foreground = t.OverlayForeground, BorderBrush = t.OverlayBorder });
        editor.Children.Add(Button("Add selected mod", "AddRule", t));
        editor.Children.Add(Text("MOD · APPLIES TO · MARK", t, 12, true, true));
        editor.Children.Add(Text("Classes: comma-separated exact names, e.g. Body Armours, Shields. Blank means all items.", t, 12, muted: true));
        var rows = new StackPanel { Name = "RuleRows" };
        if (model.Settings.Profiles.Length > 0)
        foreach (var row in ItemRulesRow.From(model.Settings.Profiles[model.Settings.ActiveProfile]))
            rows.Children.Add(BuildRow(row, rows.Children.Count, t));
        editor.Children.Add(rows);
        columns.Children.Add(Card(editor, t));
        var right = new StackPanel { Width = 340 };
        right.Children.Add(Text("PREVIEW", t, 12, true, true));
        right.Children.Add(Text("Your last copied item, with these rules", t, 12, muted: true));
        right.Children.Add(new ContentControl { Name = "ItemPreview", Content = Preview(model.Preview, t) });
        right.Children.Add(Text("CARD SHOWS", t, 12, true, true));
        right.Children.Add(Toggle("Compare", "Compare with equipped", model.Settings.ShowCompare, t));
        right.Children.Add(Toggle("Defence", "Defence base percentile", model.Settings.ShowDefenceBasePercentile, t));
        right.Children.Add(Toggle("Level", "ilvl column", model.Settings.ShowItemLevelColumn, t));
        right.Children.Add(Toggle("Implicits", "Implicits", model.Settings.ShowImplicits, t));
        right.Children.Add(Text("OMNI KEY", t, 12, true, true));
        right.Children.Add(Toggle("Advanced", "Use advanced copy (needed for tiers and rolls)", model.Omni.UseAdvancedCopy, t));
        columns.Children.Add(Card(right, t)); root.Children.Add(columns);
        columns.SizeChanged += (_, _) =>
        {
            editor.Width = Math.Min(540, Math.Max(200, columns.ActualWidth - 54));
            right.Width = Math.Min(340, Math.Max(200, columns.ActualWidth - 54));
        };
        var buttons = new WrapPanel();
        buttons.Children.Add(Button("Save changes", "SaveRules", t));
        buttons.Children.Add(Button("Discard changes", "DiscardRules", t));
        root.Children.Add(buttons);
        root.Children.Add(new TextBlock { Name = "RulesNotice", Foreground = t.TooltipLow, TextWrapping = TextWrapping.Wrap });
        return root;
    }
    public static FrameworkElement Preview(ItemCard? card, CardTheme t) => card is null
        ? Text("No copied item yet. Use the omni key on an item in game to preview it here.", t, muted: true)
        : new Viewbox { Stretch = System.Windows.Media.Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            Child = ItemCardView.Build(card, t), HorizontalAlignment = HorizontalAlignment.Left };
    private static FrameworkElement BuildRow(ItemRulesRow row, int index, CardTheme t)
    {
        var panel = new WrapPanel { Tag = row, Margin = new Thickness(0, 8, 0, 8) };
        var label = Text(row.Text + (row.Implicit ? " (implicit)" : row.Enchant ? " (enchant)" : ""), t);
        label.Width = 180; panel.Children.Add(label);
        var classes = Field("Classes", row.Classes, t); classes.Width = 160; panel.Children.Add(classes);
        foreach (var (labelText, value) in new[] { ("Want", Highlight.Desired), ("Avoid", Highlight.Undesired) })
            panel.Children.Add(Segment(labelText, value, row.Match, index, t));
        panel.Children.Add(Button("×", $"Remove{index}", t));
        return panel;
    }
    private static RadioButton Segment(string label, Highlight value, Highlight selected, int index, CardTheme t)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BorderBrushProperty, t.OverlayBorder);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.PaddingProperty, new Thickness(8));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        var template = new ControlTemplate(typeof(RadioButton)) { VisualTree = border };
        var button = new RadioButton { Content = label, Tag = value, GroupName = $"Mark{index}",
            IsChecked = selected == value, Template = template, Margin = new Thickness(0, 4, 0, 0) };
        void Paint()
        {
            button.Background = button.IsChecked == true ? t.RollTrack : t.AppNav;
            button.Foreground = button.IsChecked == true
                ? value == Highlight.Desired ? t.Accent : t.TooltipLow : t.TextMuted;
        }
        button.Checked += (_, _) => Paint(); button.Unchecked += (_, _) => Paint(); Paint();
        return button;
    }
    private static CheckBox Toggle(string name, string label, bool value, CardTheme t) => new()
    { Name = name, Content = label, IsChecked = value, Foreground = t.OverlayForeground, Margin = new Thickness(0, 4, 0, 12) };
    private static TextBox Field(string name, string value, CardTheme t) => new()
    { Name = name, Text = value, Background = t.AppNav, Foreground = t.OverlayForeground,
        BorderBrush = t.OverlayBorder, Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 8) };
}
