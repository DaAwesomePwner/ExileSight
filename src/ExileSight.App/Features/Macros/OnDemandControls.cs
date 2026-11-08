using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExileSight.App.Ui;

namespace ExileSight.App.Features.Macros;

/// <summary>Service-free controls for the three interactive on-demand surfaces.</summary>
public static class OnDemandControls
{
    public static Button Button(object content, CardTheme theme, bool pill = false, bool selected = false)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(pill ? 14 : 3));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BorderBrushProperty, theme.Accent));
        template.Triggers.Add(hover);
        return new Button
        {
            Content = content, Template = template, Focusable = false,
            Foreground = selected ? theme.Accent : theme.OverlayForeground,
            Background = pill || selected ? theme.OverlayRaised : theme.OverlayBackground,
            BorderBrush = selected ? theme.Accent : pill ? theme.OverlayBorder : theme.OverlayBackground,
            BorderThickness = new Thickness(1), Padding = new Thickness(8, 6, 8, 6),
            FontFamily = theme.TextFont, FontSize = 13
        };
    }

    public static IEnumerable<Button> Buttons(DependencyObject root)
    {
        if (root is Button button) { yield return button; yield break; }
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Buttons(child)) yield return descendant;
    }

    public static Grid Row(string label, string detail, CardTheme theme, bool mono = false)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        var name = CardPrimitives.Text(label, theme);
        name.TextWrapping = TextWrapping.Wrap;
        name.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(name);
        var value = CardPrimitives.Text(detail, theme, "TextFaint", 11, mono);
        value.MaxWidth = 130;
        value.TextTrimming = TextTrimming.CharacterEllipsis;
        value.ToolTip = detail;
        Grid.SetColumn(value, 1);
        row.Children.Add(value);
        return row;
    }
}
