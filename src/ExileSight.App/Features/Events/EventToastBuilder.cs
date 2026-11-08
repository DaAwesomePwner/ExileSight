using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;

namespace ExileSight.App.Features.Events;

public sealed record EventToastCard(string DisplayName, string Category);

public static class EventToastBuilder
{
    public static FrameworkElement Build(EventToastCard model, CardTheme theme)
    {
        var content = new DockPanel();
        var strip = new Border { Width = 4, Background = theme.OverlayAccent,
            CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 12, 0) };
        DockPanel.SetDock(strip, Dock.Left); content.Children.Add(strip);
        var lines = new StackPanel();
        var name = CardPrimitives.Text(model.DisplayName, theme);
        name.FontWeight = FontWeights.Bold; name.TextWrapping = TextWrapping.Wrap;
        lines.Children.Add(name);
        var category = CardPrimitives.Text(model.Category, theme, "TextMuted");
        category.TextWrapping = TextWrapping.Wrap; lines.Children.Add(category); content.Children.Add(lines);
        var card = CardPrimitives.CardFrame(content, theme); card.Width = 340;
        card.HorizontalAlignment = HorizontalAlignment.Left; card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }
}
