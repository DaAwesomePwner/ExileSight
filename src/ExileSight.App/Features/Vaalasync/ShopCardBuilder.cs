using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ExileSight.App.Ui;
using ExileSight.Features.Vaalasync;

namespace ExileSight.App.Features.Vaalasync;

/// <summary>Pure card tree. Tags identify controls for the host; no services or input authority.</summary>
public static class ShopCardBuilder
{
    public static FrameworkElement Build(ShopCardModel model, CardTheme theme) => new ShopCardSurface(model, theme).Frame;

    internal static FrameworkElement Header(ShopCardModel model, CardTheme theme)
    {
        var body = new StackPanel();
        var header = new DockPanel();
        var state = CardPrimitives.Text(model.Armed ? "armed" : "disarmed", theme,
            model.Armed ? "OverlayAccent" : "TextMuted", theme.LabelSize);
        DockPanel.SetDock(state, Dock.Right);
        header.Children.Add(state);
        var title = CardPrimitives.Text(model.Pasted is { } pasted ? "Pasted " + pasted : model.Name, theme);
        title.FontWeight = FontWeights.SemiBold;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.Margin = new Thickness(0, 0, 8, 0);
        header.Children.Add(title);
        body.Children.Add(header);
        if (model.Pasted is null)
        {
            var tiles = new UniformGrid { Columns = model.ListedFor is null ? 1 : 2, Margin = new Thickness(0, 10, 0, 10) };
            var current = CardPrimitives.StatTile(new("Listed now", model.ListedNow), theme);
            current.Margin = new Thickness(0, 0, model.ListedFor is null ? 0 : 6, 0);
            tiles.Children.Add(current);
            if (model.ListedFor is { } age)
                tiles.Children.Add(CardPrimitives.StatTile(new("Listed for", Duration(age)), theme));
            body.Children.Add(tiles);
        }
        return body;
    }

    internal static FrameworkElement Amount(ShopAmount amount, bool noteMode, CardTheme theme)
    {
        var column = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var value = CardPrimitives.Text(amount.Text, theme, "OverlayForeground", noteMode ? theme.LabelSize : theme.AmountSize, true);
        value.FontWeight = FontWeights.SemiBold;
        value.TextWrapping = TextWrapping.Wrap;
        value.TextAlignment = TextAlignment.Center;
        if (noteMode) value.Width = 60;
        column.Children.Add(new Viewbox
        {
            Child = value, MaxWidth = 60, Height = noteMode ? 32 : 26,
            StretchDirection = StretchDirection.DownOnly
        });
        var adjustment = CardPrimitives.Text(amount.Adjustment, theme, "TextMuted", theme.LabelSize);
        adjustment.HorizontalAlignment = HorizontalAlignment.Center;
        column.Children.Add(adjustment);
        return column;
    }

    internal static FrameworkElement Hints(ShopCardModel model, CardTheme theme)
    {
        var body = new StackPanel();
        if (model.Armed && model.Pasted is null)
            body.Children.Add(Paragraph(model.NoteMode
                ? "Open the item's price note, then pick a price"
                : "Right-click the item → Set Item Price, then pick a price", theme));
        var hint = !model.Armed ? "Omni-key the item again to re-arm." : model.NoteMode
            ? "All amounts in " + model.Currency + ". A click pastes the note into the note box you opened."
            : "All amounts in " + model.Currency + ". A click pastes the number into the amount box you opened. You pick the currency and press List.";
        body.Children.Add(Paragraph(hint, theme));
        if (!string.IsNullOrWhiteSpace(model.Notice)) body.Children.Add(Paragraph(model.Notice, theme));
        return body;
    }

    private static TextBlock Paragraph(string text, CardTheme theme)
    {
        var result = CardPrimitives.Text(text, theme, "TextSecondary", theme.LabelSize);
        result.TextWrapping = TextWrapping.Wrap;
        result.Margin = new Thickness(0, 10, 0, 0);
        return result;
    }

    private static string Duration(TimeSpan age) => age.TotalDays >= 1
        ? FormattableString.Invariant($"{age.Days} d {age.Hours} h")
        : age.TotalHours >= 1 ? FormattableString.Invariant($"{age.Hours} h {age.Minutes} m")
        : Math.Max(0, (int)age.TotalMinutes).ToString(CultureInfo.InvariantCulture) + " m";

    internal static Button Button(FrameworkElement content, string tag, CardTheme theme) => new()
    {
        Content = content, Tag = tag, Template = SharedTemplate, MinHeight = 32, Focusable = false,
        Background = theme.OverlayRaised, BorderBrush = theme.OverlayBorder
    };

    private static readonly ControlTemplate SharedTemplate = CreateTemplate();

    private static ControlTemplate CreateTemplate()
    {
        // Own the template to avoid OS chrome, animations and per-control brushes.
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(4));
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .5));
        template.Triggers.Add(disabled);
        template.Seal();
        return template;
    }
}
