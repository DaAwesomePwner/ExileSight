using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using ExileSight.App.Ui;

namespace ExileSight.App.Features.Macros;

public sealed record MacroWheelPresentation(IReadOnlyList<string> Commands, int HighlightedSlot = -1);

public static class MacroWheelBuilder
{
    public static FrameworkElement Build(MacroWheelPresentation model, CardTheme theme)
    {
        var canvas = new Canvas { Width = 400, Height = 300 };
        var ring = new Ellipse { Width = 200, Height = 200, Stroke = theme.OverlayBorder, StrokeThickness = 1 };
        Canvas.SetLeft(ring, 100); Canvas.SetTop(ring, 50); canvas.Children.Add(ring);
        var centre = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        foreach (var text in new[] { "choose", "click" })
        {
            var label = CardPrimitives.Text(text, theme, "TextMuted", 11);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            centre.Children.Add(label);
        }
        var hub = new Border { Width = 80, Height = 80, CornerRadius = new CornerRadius(40),
            Background = theme.OverlayBackground, BorderBrush = theme.OverlayBorder,
            BorderThickness = new Thickness(1), Child = centre };
        Canvas.SetLeft(hub, 160); Canvas.SetTop(hub, 110); canvas.Children.Add(hub);
        for (var i = 0; i < 8; i++)
        {
            var command = model.Commands[i];
            var label = CardPrimitives.Text(string.IsNullOrWhiteSpace(command) ? "Empty" : command,
                theme, "OverlayForeground", 11, true);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var button = OnDemandControls.Button(label, theme, pill: true);
            button.Width = 86; button.Height = theme.RowHeight; button.Tag = i;
            button.Padding = new Thickness(6, 4, 6, 4);
            button.ToolTip = command; button.IsEnabled = !string.IsNullOrWhiteSpace(command);
            var angle = i * Math.PI / 4 - Math.PI / 2;
            Canvas.SetLeft(button, 157 + Math.Cos(angle) * 100);
            Canvas.SetTop(button, 136 + Math.Sin(angle) * 100);
            Highlight(button, i == model.HighlightedSlot, theme);
            canvas.Children.Add(button);
        }
        var edit = OnDemandControls.Button(CardPrimitives.Text("Edit commands", theme, "TextMuted", 11), theme);
        edit.Tag = "edit"; edit.Width = 112;
        Canvas.SetLeft(edit, 144); Canvas.SetTop(edit, 270); canvas.Children.Add(edit);
        return canvas;
    }

    public static void Highlight(Button button, bool selected, CardTheme theme)
    {
        button.SetValue(OnDemandControls.HighlightedProperty, selected);
        ((TextBlock)button.Content).Foreground = selected ? theme.Accent :
            button.IsEnabled ? theme.OverlayForeground : theme.TextMuted;
    }

    public static int Direction(Point point)
    {
        var x = point.X - 200; var y = point.Y - 150;
        if (x * x + y * y < 40 * 40) return -1;
        return ((int)Math.Floor((Math.Atan2(y, x) + Math.PI / 2) / (Math.PI / 4) + .5) + 8) % 8;
    }
}
