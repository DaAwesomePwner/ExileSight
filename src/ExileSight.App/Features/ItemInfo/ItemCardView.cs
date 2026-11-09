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
        // Measure direct children at the same width the frame will give its content.
        // Never remeasure an ancestor after descendant removals: its cached size may be stale.
        var constraint = new Size(theme.CardWidth - frame.Padding.Left - frame.Padding.Right -
            frame.BorderThickness.Left - frame.BorderThickness.Right, double.PositiveInfinity);
        double Measure(FrameworkElement element)
        { element.Measure(constraint); return element.DesiredSize.Height; }
        var fixedHeight = frame.Padding.Top + frame.Padding.Bottom + frame.BorderThickness.Top + frame.BorderThickness.Bottom;
        void AddFixed(FrameworkElement element)
        { panel.Children.Add(element); fixedHeight += Measure(element); }
        AddFixed(ItemCardSections.Header(card, theme));
        if (card.Verdict is { } verdict)
        {
            var band = verdict.AverageRoll is { } roll
                ? CardPrimitives.VerdictBand(new UniqueVerdictModel((double)roll / 100), theme)
                : CardPrimitives.VerdictBand(new VerdictModel(verdict.Wanted, verdict.Unwanted), theme);
            band.Margin = new Thickness(0, 6, 0, 0);
            AddFixed(band);
        }
        if (card.NeedsAdvancedCopy) AddFixed(AdvancedHint(theme));
        if (!string.IsNullOrEmpty(notice))
        {
            var status = CardPrimitives.Text(notice, theme, "TextMuted", theme.LabelSize);
            status.TextTrimming = TextTrimming.CharacterEllipsis;
            AddFixed(status);
        }
        FrameworkElement? slots = null;
        if (!card.Unidentified && (card.Slots is not null || card.Defences.Any(d => d.Percentile is not null)))
        { slots = ItemCardSections.Slots(card, theme); slots.Tag = "slots"; panel.Children.Add(slots); }
        var rows = new StackPanel();
        if (card.Rows.Count > 0) panel.Children.Add(rows);
        var rowElements = new List<(FrameworkElement Element, double Height)>();
        for (var i = 0; i < card.Rows.Count; i++)
        {
            var group = new StackPanel();
            if (i == 0 || card.Rows[i - 1].Slot == CardSlotKind.Implicit && card.Rows[i].Slot != CardSlotKind.Implicit)
                group.Children.Add(CardPrimitives.Hairline(theme));
            group.Children.Add(ItemCardRows.Build(card.Rows[i], card.ShowItemLevelColumn, theme));
            rows.Children.Add(group); rowElements.Add((group, Measure(group)));
        }
        FrameworkElement? compare = null;
        if (card.Compare is { Slots.Count: > 0 } comparison)
        { compare = ItemCardSections.Compare(comparison, theme); compare.Tag = "compare"; panel.Children.Add(compare); }
        var slotsHeight = slots is null ? 0 : Measure(slots);
        var compareHeight = compare is null ? 0 : Measure(compare);
        var totalHeight = fixedHeight + slotsHeight + compareHeight + rowElements.Sum(row => row.Height);
        if (totalHeight > maxHeight && compare is not null)
        { panel.Children.Remove(compare); totalHeight -= compareHeight; }
        if (totalHeight > maxHeight && slots is not null)
        { panel.Children.Remove(slots); totalHeight -= slotsHeight; }
        if (totalHeight > maxHeight && rowElements.Count > 0)
        {
            var more = CardPrimitives.Text($"+{rowElements.Count} more mods", theme, "TextMuted", theme.LabelSize);
            more.Height = theme.RowHeight;
            rows.Children.Add(more);
            var remaining = maxHeight - fixedHeight - Measure(more);
            var kept = 0;
            foreach (var row in rowElements)
            {
                if (row.Height > remaining) break;
                remaining -= row.Height;
                kept++;
            }
            for (var i = kept; i < rowElements.Count; i++) rows.Children.Remove(rowElements[i].Element);
            more.Text = $"+{rowElements.Count - kept} more mods";
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
            theme, "TextSecondary", theme.BodySize);
        text.TextWrapping = TextWrapping.Wrap; text.Margin = new Thickness(8);
        grid.Children.Add(text);
        return grid;
    }
}
