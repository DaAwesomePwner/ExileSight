using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ExileSight.App.Ui;
using ExileSight.Features.Guide;

namespace ExileSight.App.Features.Guide;

public static class GuideCardBuilder
{
    public const int MaximumLines = 12;
    private const double CardWidth = 340;

    public static FrameworkElement Build(GuideSnapshot model, CardTheme theme)
    {
        var body = new StackPanel();
        if (model.Act is { } act)
        {
            var header = new DockPanel();
            var time = CardPrimitives.Text($"{Time(model.ActTime)} · {Time(model.TotalTime)}", theme, "TextMuted", theme.NumberSize, true);
            time.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(time, Dock.Right); header.Children.Add(time);
            var title = CardPrimitives.Text("", theme, "TextSecondary", theme.LabelSize);
            title.Inlines.Add(new Run($"Act {act}") { Foreground = theme.OverlayAccent, FontWeight = FontWeights.SemiBold });
            title.Inlines.Add(new Run($" · step {model.Step} of {model.StepCount}"));
            header.Children.Add(title); body.Children.Add(header);
            body.Children.Add(new Border
            {
                Height = 3, Background = theme.RollTrack, Margin = new Thickness(0, 8, 0, 8),
                Child = new Border { Width = (CardWidth - theme.CardPadding.Left - theme.CardPadding.Right - 2) * Math.Clamp(model.Progress, 0, 1), Background = theme.OverlayAccent,
                    HorizontalAlignment = HorizontalAlignment.Left }
            });
        }
        foreach (var line in model.Lines.Take(MaximumLines))
        {
            var text = CardPrimitives.Text(SingleLine(line), theme, size: theme.GuideStepSize);
            text.FontWeight = FontWeights.SemiBold; text.TextTrimming = TextTrimming.CharacterEllipsis;
            body.Children.Add(text);
        }
        if (model.Lines.Count > MaximumLines) body.Children.Add(Detail($"+{model.Lines.Count - MaximumLines} more lines", theme));
        if (model.NextLine is { } next) body.Children.Add(Detail("NEXT  " + next, theme));
        if (model.Notice is { } notice)
        {
            var line = Detail(notice, theme);
            line.TextWrapping = TextWrapping.NoWrap; line.TextTrimming = TextTrimming.CharacterEllipsis;
            body.Children.Add(line);
        }
        var card = CardPrimitives.CardFrame(body, theme); card.Width = CardWidth;
        card.HorizontalAlignment = HorizontalAlignment.Left; card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }

    private static TextBlock Detail(string value, CardTheme theme)
    {
        var text = CardPrimitives.Text(SingleLine(value), theme, "TextMuted");
        text.TextTrimming = TextTrimming.CharacterEllipsis; text.Margin = new Thickness(0, 8, 0, 0);
        return text;
    }

    private static string SingleLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ');

    private static string Time(TimeSpan value) => value.TotalHours >= 1
        ? string.Create(CultureInfo.InvariantCulture, $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}")
        : value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
}
