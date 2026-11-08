using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Features.Macros;
using ExileSight.App.Ui;
using ExileSight.Features.Omni;

namespace ExileSight.App.Features;

/// <summary>Pure presentation; the host attaches execution to tagged buttons.</summary>
public static class OmniMenuBuilder
{
    public static FrameworkElement Build(OmniMenuPresentation model, CardTheme theme)
    {
        var panel = new StackPanel();
        var title = CardPrimitives.Text(model.ItemName, theme, "TextSecondary", 11);
        title.TextWrapping = TextWrapping.Wrap;
        title.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(title);
        foreach (var entry in model.Entries)
        {
            var button = OnDemandControls.Button(OnDemandControls.Row(entry.Label, "›", theme), theme);
            button.Tag = entry;
            panel.Children.Add(button);
        }
        if (model.Entries.Count > 0 && model.Links.Count > 0) panel.Children.Add(CardPrimitives.Hairline(theme));
        foreach (var link in model.Links)
        {
            var button = OnDemandControls.Button(OnDemandControls.Row(link.Label, "↗", theme), theme);
            button.Tag = link;
            panel.Children.Add(button);
        }
        var frame = CardPrimitives.CardFrame(new ScrollViewer
        { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false }, theme);
        frame.Width = 260;
        return frame;
    }
}
