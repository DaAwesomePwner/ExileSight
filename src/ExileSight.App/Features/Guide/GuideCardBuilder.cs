using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ExileSight.App.Ui;
using ExileSight.Features.Guide;

namespace ExileSight.App.Features.Guide;

public static class GuideCardBuilder
{
    public static FrameworkElement Build(GuideSnapshot model, CardTheme theme)
    {
        var body = new StackPanel();
        if (model.Act is { } act)
        {
            var header = new DockPanel();
            var time = CardPrimitives.Text($"{Time(model.ActTime)} · {Time(model.TotalTime)}", theme, "TextMuted", 11, true);
            time.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(time, Dock.Right); header.Children.Add(time);
            var title = CardPrimitives.Text("", theme, "TextSecondary", 11);
            title.Inlines.Add(new Run($"Act {act}") { Foreground = theme.OverlayAccent, FontWeight = FontWeights.SemiBold });
            title.Inlines.Add(new Run($" · step {model.Step} of {model.StepCount}"));
            header.Children.Add(title); body.Children.Add(header);
            body.Children.Add(new Border
            {
                Height = 3, Background = theme.RollTrack, Margin = new Thickness(0, 8, 0, 8),
                Child = new Border { Width = 314 * Math.Clamp(model.Progress, 0, 1), Background = theme.OverlayAccent,
                    HorizontalAlignment = HorizontalAlignment.Left }
            });
        }
        foreach (var line in model.Lines)
        {
            var text = CardPrimitives.Text(line, theme, size: 15);
            text.FontWeight = FontWeights.SemiBold; text.TextWrapping = TextWrapping.Wrap;
            body.Children.Add(text);
        }
        if (model.NextLine is { } next) body.Children.Add(Detail("NEXT  " + next, theme));
        if (model.Notice is { } notice)
        {
            var line = Detail(notice, theme);
            line.TextWrapping = TextWrapping.NoWrap; line.TextTrimming = TextTrimming.CharacterEllipsis;
            body.Children.Add(line);
        }
        var card = CardPrimitives.CardFrame(body, theme); card.Width = 340;
        card.HorizontalAlignment = HorizontalAlignment.Left; card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }

    private static TextBlock Detail(string value, CardTheme theme)
    {
        var text = CardPrimitives.Text(value, theme, "TextMuted");
        text.TextWrapping = TextWrapping.Wrap; text.Margin = new Thickness(0, 8, 0, 0);
        return text;
    }

    private static string Time(TimeSpan value) => value.TotalHours >= 1
        ? string.Create(CultureInfo.InvariantCulture, $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}")
        : value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
}
