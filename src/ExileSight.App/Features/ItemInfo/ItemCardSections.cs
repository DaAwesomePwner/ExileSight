using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.Core.Features;
using ExileSight.Features.ItemInfo;

namespace ExileSight.App.Features.ItemInfo;

internal static class ItemCardSections
{
    internal static FrameworkElement Header(ItemCard card, CardTheme theme)
    {
        var panel = new StackPanel();
        var row = new DockPanel();
        if (card.Name.ItemLevel is { } level)
        {
            var text = CardPrimitives.Text($"ilvl {level}", theme, "TextMuted", 11, true);
            DockPanel.SetDock(text, Dock.Right);
            row.Children.Add(text);
        }
        var name = CardPrimitives.Text(card.Name.DisplayName, theme,
            card.Name.UniqueName is null ? "OverlayForeground" : "TooltipUnique");
        name.FontWeight = FontWeights.SemiBold;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        row.Children.Add(name);
        panel.Children.Add(row);
        var subtitle = card.Unidentified ? "Unidentified · mods hidden" :
            $"{card.Name.Rarity} · {card.Name.ItemClass}" +
            (card.Name.UniqueName is null || card.Name.BaseType is null ? "" : $" · {card.Name.BaseType}");
        var sub = CardPrimitives.Text(subtitle, theme, "TextMuted", 11);
        sub.TextTrimming = TextTrimming.CharacterEllipsis;
        sub.Tag = "subtitle";
        if (card.Unidentified && card.Defences.Any(d => d.Percentile is not null))
            panel.Children.Add(SideBySide(sub, Defences(card, theme, false)));
        else panel.Children.Add(sub);
        return panel;
    }

    internal static FrameworkElement Slots(ItemCard card, CardTheme theme)
    {
        FrameworkElement left = new Border();
        if (card.Slots is { } slots)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Height = 18, Tag = "slot-pips" };
            void Pips(string label, ItemCardPips pips)
            {
                row.Children.Add(CardPrimitives.Text(label + " ", theme, "TextSecondary", 11));
                var states = Enumerable.Range(0, pips.Maximum ?? 3).Select(i =>
                    pips.Count is not int count || pips.Maximum is null ? SlotState.Unknown :
                    i < count ? SlotState.Used : SlotState.Open);
                var dots = CardPrimitives.SlotPips(states, theme);
                dots.VerticalAlignment = VerticalAlignment.Center;
                dots.Margin = new Thickness(0, 0, 5, 0);
                row.Children.Add(dots);
            }
            Pips("P", slots.Prefixes); Pips("S", slots.Suffixes);
            if (slots.Prefixes.Open is int p && slots.Suffixes.Open is int s && p + s > 0)
                row.Children.Add(CardPrimitives.Text($"{p + s} open", theme, "OverlayAccent", 11));
            left = row;
        }
        var panel = SideBySide(left, Defences(card, theme, true));
        panel.Margin = new Thickness(0, 6, 0, 0);
        return panel;
    }

    private static Grid SideBySide(FrameworkElement left, FrameworkElement right)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        left.Margin = new Thickness(0, 0, 8, 0);
        left.VerticalAlignment = VerticalAlignment.Top;
        left.ClipToBounds = true;
        right.HorizontalAlignment = HorizontalAlignment.Right;
        right.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(right, 1);
        row.Children.Add(left); row.Children.Add(right);
        return row;
    }

    private static FrameworkElement Defences(ItemCard card, CardTheme theme, bool showBar)
    {
        var panel = new StackPanel { Tag = "base-percentiles", MaxWidth = 130 };
        foreach (var defence in card.Defences.Where(d => d.Percentile is not null))
        {
            var row = new Grid { Height = 18 };
            if (showBar) row.Width = 130;
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var label = CardPrimitives.Text((defence.Name == "Energy Shield" ? "ES" : defence.Name) + " base", theme, "TextMuted", 11);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.Margin = new Thickness(0, 0, 4, 0);
            row.Children.Add(label);
            if (showBar)
            {
                var bar = CardPrimitives.RollBar(new((double)defence.Percentile!.Value / 100, CardTone.High), theme);
                Grid.SetColumn(bar, 1); row.Children.Add(bar);
            }
            var number = CardPrimitives.Text(Ordinal(defence.Percentile!.Value), theme, "TextSecondary", 11, true);
            number.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(number, 2); row.Children.Add(number);
            panel.Children.Add(row);
        }
        return panel;
    }

    private static string Ordinal(decimal value)
    {
        var rounded = (int)decimal.Round(value, 0, MidpointRounding.AwayFromZero);
        var suffix = rounded % 100 is 11 or 12 or 13 ? "th" : (rounded % 10) switch
        { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return rounded.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    internal static FrameworkElement Compare(ItemCardComparison comparison, CardTheme theme)
    {
        var panel = new StackPanel();
        panel.Children.Add(CardPrimitives.Hairline(theme));
        foreach (var slot in comparison.Slots)
        {
            panel.Children.Add(CardPrimitives.Text("vs " + slot.Slot, theme, "TextMuted", 11));
            if (slot.NothingEquipped)
                panel.Children.Add(CardPrimitives.Text("nothing equipped", theme, "TextMuted", 11));
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new());
            for (var i = 0; i < slot.Deltas.Count; i++)
            {
                if (i % 2 == 0) grid.RowDefinitions.Add(new() { Height = new GridLength(22) });
                var delta = slot.Deltas[i];
                var cell = new DockPanel { Margin = new Thickness(0, 0, 5, 0) };
                var value = CardPrimitives.Delta((double)delta.AbsoluteValue *
                    (delta.Direction == DeltaDirection.Decrease ? -1 : 1), theme);
                DockPanel.SetDock(value, Dock.Right); cell.Children.Add(value);
                var label = CardPrimitives.Text(delta.Stat, theme, "TextSecondary", 11);
                label.TextTrimming = TextTrimming.CharacterEllipsis; cell.Children.Add(label);
                Grid.SetColumn(cell, i % 2); Grid.SetRow(cell, i / 2); grid.Children.Add(cell);
            }
            panel.Children.Add(grid);
        }
        var note = CardPrimitives.Text(comparison.Note, theme, "TextMuted", 10);
        note.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(note);
        return panel;
    }
}
