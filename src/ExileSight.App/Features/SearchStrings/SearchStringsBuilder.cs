using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Features.Macros;
using ExileSight.App.Ui;

namespace ExileSight.App.Features.SearchStrings;

public sealed record SearchRowPresentation(string Name, string Text, int Line, int LineCount);
public sealed record SearchStringsPresentation(IReadOnlyList<string> Vendors, string? SelectedVendor,
    IReadOnlyList<SearchRowPresentation> Entries, string? Notice = null);

public static class SearchStringsBuilder
{
    public static FrameworkElement Build(SearchStringsPresentation model, CardTheme theme)
    {
        var panel = new StackPanel();
        var chips = new WrapPanel();
        foreach (var vendor in model.Vendors)
        {
            var selected = vendor == model.SelectedVendor;
            var label = CardPrimitives.Text(vendor, theme, selected ? "Accent" : "OverlayForeground", theme.LabelSize);
            var button = OnDemandControls.Button(label, theme, pill: true, selected: selected);
            button.Tag = vendor; button.Margin = new Thickness(0, 0, 6, 6);
            chips.Children.Add(button);
        }
        panel.Children.Add(chips);
        panel.Children.Add(CardPrimitives.Hairline(theme));
        if (model.Notice is { } notice)
        {
            var label = CardPrimitives.Text(notice, theme, "TextMuted", theme.LabelSize);
            label.TextWrapping = TextWrapping.Wrap;
            panel.Children.Add(label);
        }
        for (var i = 0; i < model.Entries.Count; i++)
        {
            var button = OnDemandControls.Button(Row(model.Entries[i], theme), theme);
            button.Tag = i;
            panel.Children.Add(button);
        }
        var footer = new StackPanel { Orientation = Orientation.Horizontal };
        if (model.SelectedVendor is not null)
        {
            var back = OnDemandControls.Button(CardPrimitives.Text("Back to vendors", theme, "TextMuted", theme.LabelSize), theme);
            back.Name = "Back"; footer.Children.Add(back);
        }
        var close = OnDemandControls.Button(CardPrimitives.Text("Close", theme, "TextMuted", theme.LabelSize), theme);
        close.Name = "Close"; footer.Children.Add(close);
        panel.Children.Add(footer);
        var frame = CardPrimitives.CardFrame(new ScrollViewer
        { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false }, theme);
        frame.Width = 320;
        return frame;
    }

    public static FrameworkElement Row(SearchRowPresentation model, CardTheme theme)
    {
        var row = OnDemandControls.Row(model.Name, model.Text, theme, mono: true);
        if (model.LineCount <= 1) return row;
        var panel = new StackPanel();
        panel.Children.Add(row);
        var hint = CardPrimitives.Text($"wheel ↕ {model.Line}/{model.LineCount}", theme, "TextFaint", theme.NumberSize, true);
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        panel.Children.Add(hint);
        return panel;
    }
}
