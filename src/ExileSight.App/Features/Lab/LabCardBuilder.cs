using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Features.Macros;
using ExileSight.App.Ui;
using ExileSight.Features.Lab;

namespace ExileSight.App.Features.Lab;

/// <summary>Detached Lab presentation; time and all external text are supplied by the host.</summary>
public static class LabCardBuilder
{
    public static Border Build((LabLayout? Layout, string? RoomId, string Tracking, string Difficulty) state,
        string error, string tips, DateTimeOffset now, CardTheme theme)
    {
        var content = new StackPanel();
        var title = Text("Labyrinth", theme);
        title.FontWeight = FontWeights.SemiBold;
        content.Children.Add(title);
        content.Children.Add(Text("Import the daily compass JSON", theme, "TextMuted"));
        var controls = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var difficulty = new ComboBox
        {
            Name = "Difficulty", ItemsSource = LabImport.Difficulties, SelectedItem = state.Difficulty,
            Width = 100, FontFamily = theme.TextFont, FontSize = theme.BodySize,
            Margin = new Thickness(0, 0, 4, 4), MinHeight = theme.RowHeight
        };
        System.Windows.Automation.AutomationProperties.SetName(difficulty, "Lab difficulty");
        controls.Children.Add(difficulty);
        foreach (var label in new[] { "Open poelab", "Import file…", "Paste JSON", "Close" })
        {
            var button = OnDemandControls.Button(label, theme);
            button.Tag = label;
            button.Margin = new Thickness(0, 0, 4, 4);
            button.MinHeight = theme.RowHeight;
            controls.Children.Add(button);
        }
        content.Children.Add(controls);
        content.Children.Add(CardPrimitives.Hairline(theme));
        if (error.Length > 0) content.Children.Add(Text("⚠ " + error, theme, "TooltipLow"));
        if (state.Layout is { } layout)
        {
            content.Children.Add(Text($"{layout.Difficulty} · {layout.Date:yyyy-MM-dd}", theme, "TextSecondary"));
            if (layout.IsOutdated(now))
            {
                var warning = Text("⚠ OUTDATED · daily reset 00:00 UTC", theme, "TooltipLow");
                warning.FontWeight = FontWeights.SemiBold;
                content.Children.Add(warning);
            }
            content.Children.Add(Text(state.Tracking, theme));
            content.Children.Add(Text($"Weapon: {layout.Weapon} · Phases: {layout.Phase1}, {layout.Phase2}", theme, "TextSecondary"));
            content.Children.Add(Text($"Traps: {layout.Trap1}, {layout.Trap2}", theme, "TextSecondary"));
            var map = new Canvas { Width = 418, Height = 265, HorizontalAlignment = HorizontalAlignment.Left };
            LabMap.Draw(map, layout, state.RoomId, theme);
            content.Children.Add(map);
            content.Children.Add(Text("◎ current room · ◆ darkshrine · K golden key · D golden door · A Argus · ⚠ traps/gauntlet", theme, "TextMuted", theme.LabelSize));
        }
        else
        {
            content.Children.Add(Text(state.Tracking, theme, "TextSecondary"));
            content.Children.Add(Text("No saved layout for this difficulty. Import a compass JSON.", theme));
        }
        content.Children.Add(CardPrimitives.Hairline(theme));
        var heading = Text("TRIALS & TIPS", theme, "TextMuted", theme.LabelSize);
        heading.FontWeight = FontWeights.SemiBold;
        content.Children.Add(heading);
        content.Children.Add(new ScrollViewer { Height = 110, Content = Text(tips, theme, "TextSecondary"),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var frame = CardPrimitives.CardFrame(content, theme);
        frame.Width = 444;
        frame.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/ExileSight.App;component/Ui/Tokens.xaml", UriKind.Relative) });
        frame.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/ExileSight.App;component/Shell/ShellControls.xaml", UriKind.Relative) });
        foreach (var key in new[] { "TextSecondary", "TextMuted", "OverlayBorder", "OverlayRule" })
            frame.Resources[key] = theme[key];
        return frame;
    }

    private static TextBlock Text(string value, CardTheme theme, string key = "OverlayForeground", double size = 13)
    {
        var text = CardPrimitives.Text(value, theme, key, size);
        text.TextWrapping = TextWrapping.Wrap;
        return text;
    }
}
