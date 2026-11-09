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
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        return new Border { Height = 14, Padding = new Thickness(2, 0, 2, 0),
            Background = Brush(tag.Color), Child = text, ClipToBounds = true, IsHitTestVisible = false };
    }

    private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;

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
        string age, CardTheme theme)
    {
        var visuals = new List<Visual>();
        foreach (var tag in tags)
        {
            var element = Tag(tag, theme);
            element.Measure(new Size(double.PositiveInfinity, 14));
            var bounds = StashGeometry.CornerTagBounds(layout, tag.Cell, transform, element.DesiredSize.Width);
            element.LayoutTransform = new ScaleTransform(transform.Scale / scale, transform.Scale / scale);
            element.Width = bounds.Width / transform.Scale;
            visuals.Add(new(element, bounds));
        }
        if (tags.Length == 0) return visuals;
        var card = Summary(tags, age, theme);
        card.Measure(new Size(card.Width, double.PositiveInfinity));
        var width = card.Width * scale; var height = card.DesiredSize.Height * scale;
        // Keep the card beyond every cell in this page, including folder offsets.
        var right = layout.Cells.Max(c => transform.Apply(Layouts.Origin(layout, c)).X + c.W * transform.Scale);
        var top = layout.Cells.Min(c => transform.Apply(Layouts.Origin(layout, c)).Y);
        var left = layout.Cells.Min(c => transform.Apply(Layouts.Origin(layout, c)).X);
        var bottom = layout.Cells.Max(c => transform.Apply(Layouts.Origin(layout, c)).Y + c.H * transform.Scale);
        var gap = 20 * scale;
        var x = right + gap; var y = Math.Clamp(top, 0, Math.Max(0, resolution.Height - height));
        if (x + width > resolution.Width)
        {
            x = left - gap - width;
            if (x < 0)
            {
                x = Math.Clamp(left, 0, Math.Max(0, resolution.Width - width));
                y = bottom + gap;
                if (y + height > resolution.Height) y = top - gap - height;
            }
        }
        // No room outside the tab: omit rather than cover cells or stack counts.
        if (x >= 0 && y >= 0 && x + width <= resolution.Width && y + height <= resolution.Height)
            visuals.Add(new(card, new(x, y, width, height)));
        return visuals;
    }
}
