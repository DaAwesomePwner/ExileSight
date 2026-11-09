using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.Features.Timers;

namespace ExileSight.App.Features.Timers;

public sealed record TimerCardRow(string Name, TimerKind Kind, TimeSpan Value, TimeSpan Duration);

public static class TimersCardBuilder
{
    public static FrameworkElement Build(IReadOnlyList<TimerCardRow> rows, CardTheme theme)
    {
        var body = new StackPanel();
        foreach (var row in rows.Take(4))
        {
            var grid = new Grid { Margin = new Thickness(0, body.Children.Count == 0 ? 0 : theme.RowHeight - 20, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            var name = CardPrimitives.Text(row.Name, theme);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            left.Children.Add(name);
            var urgent = row.Kind == TimerKind.Countdown && row.Value < TimeSpan.FromMinutes(1);
            if (row.Kind == TimerKind.Countdown)
            {
                var fraction = row.Duration > TimeSpan.Zero ? Math.Clamp(row.Value / row.Duration, 0, 1) : 0;
                var bar = new Grid { Height = 4, Background = theme.RollTrack, Margin = new Thickness(0, 4, 0, 0) };
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(fraction, GridUnitType.Star) });
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - fraction, GridUnitType.Star) });
                bar.Children.Add(new Border { Background = urgent ? theme.TooltipLow : theme.TooltipHigh });
                left.Children.Add(bar);
            }
            grid.Children.Add(left);
            var value = row.Value.TotalHours >= 1
                ? string.Create(CultureInfo.InvariantCulture, $"{(int)row.Value.TotalHours}:{row.Value.Minutes:00}:{row.Value.Seconds:00}")
                : row.Value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
            var time = CardPrimitives.Text(value, theme, urgent ? "TooltipLow" :
                row.Kind == TimerKind.Stopwatch ? "OverlayAccent" : "OverlayForeground", 12, true);
            time.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(time, 1); grid.Children.Add(time); body.Children.Add(grid);
        }
        if (rows.Count == 0) body.Children.Add(CardPrimitives.Text("No timers", theme, "TextMuted"));
        if (rows.Count > 4) body.Children.Add(CardPrimitives.Text($"+{rows.Count - 4} more", theme, "TextMuted"));
        var card = CardPrimitives.CardFrame(body, theme); card.Width = 300;
        card.HorizontalAlignment = HorizontalAlignment.Left; card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }
}
