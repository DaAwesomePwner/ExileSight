using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using ExileSight.App.Ui;
using ExileSight.Features.ItemInfo;

namespace ExileSight.App.Features.ItemInfo;

/// <summary>Pure card construction. maxHeight is the client height cap in logical card units.</summary>
public static class ItemCardView
{
    public static FrameworkElement Build(ItemCard card, CardTheme theme,
        double maxHeight = double.PositiveInfinity, string? notice = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHeight);
        var panel = new StackPanel();
        var frame = CardPrimitives.CardFrame(panel, theme);
        panel.Children.Add(ItemCardSections.Header(card, theme));
        if (card.Verdict is { } verdict)
        {
            var band = verdict.AverageRoll is { } roll
                ? CardPrimitives.VerdictBand(new UniqueVerdictModel((double)roll / 100), theme)
                : CardPrimitives.VerdictBand(new VerdictModel(verdict.Wanted, verdict.Unwanted), theme);
            band.Margin = new Thickness(0, 6, 0, 0);
            panel.Children.Add(band);
        }
        if (card.NeedsAdvancedCopy) panel.Children.Add(AdvancedHint(theme));
        if (!string.IsNullOrEmpty(notice))
        {
            var status = CardPrimitives.Text(notice, theme, "TextMuted", 11);
            status.TextTrimming = TextTrimming.CharacterEllipsis;
            panel.Children.Add(status);
        }
        FrameworkElement? slots = null;
        if (card.Slots is not null || card.Defences.Any(d => d.Percentile is not null))
        { slots = ItemCardSections.Slots(card, theme); slots.Tag = "slots"; panel.Children.Add(slots); }
        var rows = new StackPanel();
        if (card.Rows.Count > 0) panel.Children.Add(rows);
        var rowElements = new List<FrameworkElement>();
        for (var i = 0; i < card.Rows.Count; i++)
        {
            var group = new StackPanel();
            if (i == 0 || card.Rows[i - 1].Slot == CardSlotKind.Implicit && card.Rows[i].Slot != CardSlotKind.Implicit)
                group.Children.Add(CardPrimitives.Hairline(theme));
            group.Children.Add(ItemCardRows.Build(card.Rows[i], card.ShowItemLevelColumn, theme));
            rows.Children.Add(group); rowElements.Add(group);
        }
        FrameworkElement? compare = null;
        if (card.Compare is { Slots.Count: > 0 } comparison)
        { compare = ItemCardSections.Compare(comparison, theme); compare.Tag = "compare"; panel.Children.Add(compare); }
        bool Fits()
        {
            frame.Measure(new Size(theme.CardWidth, double.PositiveInfinity));
            return frame.DesiredSize.Height <= maxHeight;
        }
        if (!Fits() && compare is not null) panel.Children.Remove(compare);
        if (!Fits() && slots is not null) panel.Children.Remove(slots);
        if (!Fits() && rowElements.Count > 0)
        {
            var more = CardPrimitives.Text("", theme, "TextMuted", 11);
            more.Height = theme.RowHeight;
            rows.Children.Add(more);
            var removed = 0;
            do
            {
                rows.Children.Remove(rowElements[^(++removed)]);
                more.Text = $"+{removed} more mods";
            } while (!Fits() && removed < rowElements.Count);
        }
        // Degenerate tiny clients cannot even hold the header; never draw outside their cap.
        frame.MaxHeight = maxHeight;
        frame.ClipToBounds = true;
        return frame;
    }

    private static FrameworkElement AdvancedHint(CardTheme theme)
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        var dashes = new DoubleCollection { 2, 2 }; dashes.Freeze();
        grid.Children.Add(new Rectangle { Stroke = theme.TooltipUnknown, StrokeDashArray = dashes,
            StrokeThickness = 1, RadiusX = 3, RadiusY = 3 });
        var text = CardPrimitives.Text("Tiers and rolls need advanced copy. Turn it back on in Item rules › Omni key.",
            theme, "TextSecondary", 12);
        text.TextWrapping = TextWrapping.Wrap; text.Margin = new Thickness(8);
        grid.Children.Add(text);
        return grid;
    }
}
