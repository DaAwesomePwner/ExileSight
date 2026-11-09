using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.Features.ItemInfo;

namespace ExileSight.App.Features.ItemInfo;

internal static class ItemCardRows
{
    internal static FrameworkElement Build(ItemCardRow row, bool levelColumn, CardTheme theme)
    {
        var grid = new Grid { Height = row.GameLineCount > 1 ? 40 : theme.RowHeight };
        double[] widths = [12, 12, -1, theme.TierPillWidth, 40, 30, 20];
        var count = levelColumn ? 7 : 6;
        for (var i = 0; i < count; i++)
        {
            if (i > 0) grid.ColumnDefinitions.Add(new() { Width = new GridLength(5) });
            grid.ColumnDefinitions.Add(new() { Width = widths[i] < 0 ? new(1, GridUnitType.Star) : new(widths[i]) });
        }
        void Add(FrameworkElement element, int column)
        { Grid.SetColumn(element, column * 2); grid.Children.Add(element); }
        Add(CardPrimitives.Text(row.Slot switch
        { CardSlotKind.Prefix => "P", CardSlotKind.Suffix => "S", CardSlotKind.Implicit => "I",
            CardSlotKind.Unknown => "?", _ => "" }, theme, "TextFaint", theme.LabelSize), 0);
        Add(CardPrimitives.Text(row.Highlight switch
        { Highlight.Desired => "◆", Highlight.Undesired => "✕", _ => "" }, theme,
            row.Highlight == Highlight.Undesired ? "TooltipLow" : "OverlayAccent", theme.LabelSize), 1);
        var label = CardPrimitives.Text(row.Label, theme, row.Fixed ? "TextMuted" : row.Crafted ? "TooltipCrafted" :
            row.Highlight == Highlight.Undesired ? "TextSecondary" : "OverlayForeground");
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        if (row.GameLineCount > 1)
        {
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(label);
            stack.Children.Add(CardPrimitives.Text($"{row.GameLineCount} game lines", theme, "TextMuted", theme.LabelSize));
            Add(new Border { BorderBrush = theme.TextMuted, BorderThickness = new Thickness(1, 0, 0, 0),
                Padding = new Thickness(4, 0, 0, 0), Child = stack }, 2);
        }
        else Add(label, 2);
        if (row.Tier == "·") Add(CardPrimitives.Text("·", theme, "TextMuted", theme.NumberSize, true), 3);
        else
        {
            var kind = row.Tier switch { "imp" => PillKind.Implicit, "ench" => PillKind.Enchant,
                "cr" => PillKind.Crafted, "U" => PillKind.Unique, "T?" => PillKind.Unknown, _ => PillKind.Tier };
            var tier = int.TryParse(row.Tier.AsSpan(1), out var n) ? n : (int?)null;
            Add(CardPrimitives.TierPill(new(kind, tier, row.TierTone.ToCardTone()), theme), 3);
        }
        if (row.Fixed) Add(CardPrimitives.Text("fixed", theme, "TextMuted", theme.LabelSize), 4);
        else
        {
            var roll = new RollBarModel(row.RollPosition is { } p ? (double)p / 100 : null, row.RollTone.ToCardTone());
            Add(CardPrimitives.RollBar(roll, theme), 4);
            var percent = CardPrimitives.RollLabel(roll, theme);
            percent.HorizontalAlignment = HorizontalAlignment.Right;
            Add(percent, 5);
        }
        if (levelColumn)
        {
            var level = CardPrimitives.Text(row.LevelKind switch
            { CardLevelKind.Known => row.RequiredLevel?.ToString() ?? "—", CardLevelKind.Unknown => "—", _ => "" },
                theme, "TextMuted", theme.NumberSize, true);
            level.HorizontalAlignment = HorizontalAlignment.Right;
            Add(level, 6);
        }
        return grid;
    }
}
