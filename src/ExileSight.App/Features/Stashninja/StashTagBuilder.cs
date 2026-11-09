using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ExileSight.App.Ui;
using ExileSight.Features.Stashninja;
using Bounds = ExileSight.Features.Stashninja.Geometry.Bounds;
using StashGeometry = ExileSight.Features.Stashninja.Geometry;

namespace ExileSight.App.Features.Stashninja;

/// <summary>Detached visuals, with the same physical rectangles used by recognition.</summary>
public static class StashTagBuilder
{
    public sealed record Visual(FrameworkElement Element, Bounds Bounds);

    public static Border Tag(PriceTag tag, CardTheme theme)
    {
        var text = CardPrimitives.Text(tag.Text, theme, size: 10, number: true);
        text.Foreground = Brush(TagPresentation.Foreground(tag.Color));
        text.TextTrimming = TextTrimming.None;
        return new Border { Height = 14, Padding = new Thickness(2, 0, 2, 0),
            Background = Brush(tag.Color), Child = text, ClipToBounds = true, IsHitTestVisible = false };
    }

    private static readonly Dictionary<string, SolidColorBrush> Brushes = new(StringComparer.OrdinalIgnoreCase);
    private static SolidColorBrush Brush(string color)
    {
        lock (Brushes)
        {
            if (Brushes.TryGetValue(color, out var cached)) return cached;
            var rgb = Convert.FromHexString(color[1..]);
            var brush = new SolidColorBrush(Color.FromRgb(rgb[0], rgb[1], rgb[2]));
            brush.Freeze();
            // Bound retention across repeated profile edits; at most 40 profile colours are active.
            if (Brushes.Count >= 128) Brushes.Clear();
            Brushes.Add(color, brush);
            return brush;
        }
    }

    public static Border Summary(IEnumerable<PriceTag> tags, string age, CardTheme theme)
    {
        var column = new StackPanel();
        column.Children.Add(CardPrimitives.Text("Highest unit prices · 7 days", theme, "TextSecondary", 11));
        foreach (var tag in TagPresentation.Highest(tags))
        {
            var row = new Grid { Height = 24, Margin = new Thickness(0, 4, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            var name = CardPrimitives.Text(tag.Name, theme, size: 12);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(name);
            var chart = Sparkline(tag.Sparkline, theme);
            Grid.SetColumn(chart, 1); row.Children.Add(chart);
            var price = CardPrimitives.Text(tag.Text, theme, size: 11, number: true);
            price.HorizontalAlignment = HorizontalAlignment.Right; price.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(price, 2); row.Children.Add(price);
            column.Children.Add(row);
        }
        column.Children.Add(CardPrimitives.Hairline(theme));
        column.Children.Add(CardPrimitives.Text("poe.ninja · " + age, theme, "TextMuted", 11));
        var card = CardPrimitives.CardFrame(column, theme);
        card.Width = 280;
        card.IsHitTestVisible = false;
        return card;
    }

    private static Canvas Sparkline(double?[] history, CardTheme theme)
    {
        var chart = new Canvas { Width = 44, Height = 14, VerticalAlignment = VerticalAlignment.Center };
        var values = history.Where(v => v is { } n && double.IsFinite(n)).Select(v => v!.Value).ToArray();
        if (values.Length == 0) return chart;
        var min = values.Min(); var span = Math.Max(1, values.Max() - min);
        for (var i = 1; i < Math.Min(7, history.Length); i++)
            if (history[i - 1] is { } a && history[i] is { } b && double.IsFinite(a) && double.IsFinite(b))
                chart.Children.Add(new Line { X1 = (i - 1) * 44d / 6, X2 = i * 44d / 6,
                    Y1 = 14 - (a - min) / span * 14, Y2 = 14 - (b - min) / span * 14,
                    Stroke = theme.TooltipHigh, StrokeThickness = 1 });
        return chart;
    }

    public static IReadOnlyList<Visual> Build(Layout layout, PriceTag[] tags,
        ExileSight.Features.Stashninja.Transform transform, Resolution resolution, double scale,
        string age, CardTheme theme, RecognitionRegion? recognition)
    {
        var visuals = new List<Visual>();
        foreach (var tag in tags)
        {
            // Decide fit before applying bounds. Include padding and a rounding guard
            // so the arranged text never needs trimming, even at fractional scales.
            Border? element = null;
            double tagWidth = 0;
            var chosen = TagPresentation.FitPrice(tag.Text, candidate =>
            {
                var visual = Tag(tag with { Text = candidate }, theme);
                visual.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var guard = 2 * scale / transform.Scale;
                var measured = Math.Ceiling(visual.DesiredSize.Width + guard);
                if (measured > tag.Cell.W - 4 || tag.Cell.H - 4 < 14) return false;
                element = visual; tagWidth = measured;
                return true;
            });
            if (chosen is null || element is null) continue;
            var bounds = StashGeometry.CornerTagBounds(layout, tag.Cell, transform, tagWidth);
            element.LayoutTransform = new ScaleTransform(transform.Scale / scale, transform.Scale / scale);
            element.Width = bounds.Width / transform.Scale;
            visuals.Add(new(element, bounds));
        }
        if (tags.Length == 0 || recognition is null) return visuals;
        var card = Summary(tags, age, theme);
        card.Measure(new Size(card.Width, double.PositiveInfinity));
        var width = card.Width * scale; var height = card.DesiredSize.Height * scale;
        var boundsForCard = StashSummaryPlacement.Find(layout, transform, resolution, scale, width, height, recognition);
        if (boundsForCard is not null) visuals.Add(new(card, boundsForCard));
        return visuals;
    }
}
