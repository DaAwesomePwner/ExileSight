using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ExileSight.App.Ui;

namespace ExileSight.App.Features.Vaalasync;

/// <summary>Pure card tree. Tags identify controls for the host; no services or input authority.</summary>
public static class ShopCardBuilder
{
    public static FrameworkElement Build(ShopCardModel model, CardTheme theme)
    {
        var body = new StackPanel();
        var header = new DockPanel();
        var state = CardPrimitives.Text(model.Armed ? "armed" : "disarmed", theme,
            model.Armed ? "OverlayAccent" : "TextMuted", 11);
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
        var amounts = new UniformGrid { Columns = 4, Margin = new Thickness(0, model.Pasted is null ? 0 : 8, -6, 0) };
        foreach (var amount in model.Amounts)
        {
            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var value = CardPrimitives.Text(amount.Text, theme, "OverlayForeground", model.NoteMode ? 11 : 18, true);
            value.FontWeight = FontWeights.SemiBold;
            value.TextWrapping = TextWrapping.Wrap;
            value.TextAlignment = TextAlignment.Center;
            if (model.NoteMode) value.Width = 60;
            column.Children.Add(new Viewbox
            {
                Child = value, MaxWidth = 60, Height = model.NoteMode ? 32 : 26,
                StretchDirection = StretchDirection.DownOnly
            });
            var adjustment = CardPrimitives.Text(amount.Adjustment, theme, "TextMuted", 11);
            adjustment.HorizontalAlignment = HorizontalAlignment.Center;
            column.Children.Add(adjustment);
            var button = Button(column, amount.Choice, theme);
            button.Height = 56;
            button.Margin = new Thickness(0, 0, 6, 0);
            button.IsEnabled = model.Armed && amount.Available;
            amounts.Children.Add(button);
        }
        body.Children.Add(amounts);
        if (model.Pasted is null)
        {
            var custom = Button(CardPrimitives.Text("Custom amount…", theme), "edit-custom", theme);
            custom.Margin = new Thickness(0, 10, 0, 0);
            custom.IsEnabled = model.Armed;
            body.Children.Add(custom);
            if (model.CustomAmount is { } text)
            {
                var apply = Button(CardPrimitives.Text("Apply custom · " + text, theme), "custom", theme);
                apply.IsEnabled = model.Armed;
                apply.Margin = new Thickness(0, 6, 0, 0);
                body.Children.Add(apply);
            }
        }
        var hint = !model.Armed ? "Omni-key the item again to re-arm." : model.NoteMode
            ? "All amounts in " + model.Currency + ". A click pastes the note into the note box you opened."
            : "All amounts in " + model.Currency + ". A click pastes the number into the amount box you opened. You pick the currency and press List.";
        body.Children.Add(Paragraph(hint, theme));
        if (!string.IsNullOrWhiteSpace(model.Notice)) body.Children.Add(Paragraph(model.Notice, theme));
        var footer = new UniformGrid { Columns = 2, Margin = new Thickness(0, 8, 0, 0) };
        footer.Children.Add(Button(CardPrimitives.Text("History", theme, "TextSecondary", 11), "history", theme));
        footer.Children.Add(Button(CardPrimitives.Text("Close", theme, "TextSecondary", 11), "close", theme));
        body.Children.Add(footer);
        var frame = CardPrimitives.CardFrame(body, theme);
        frame.Width = 330;
        return frame;
    }

    private static TextBlock Paragraph(string text, CardTheme theme)
    {
        var result = CardPrimitives.Text(text, theme, "TextSecondary", 11);
        result.TextWrapping = TextWrapping.Wrap;
        result.Margin = new Thickness(0, 10, 0, 0);
        return result;
    }

    private static string Duration(TimeSpan age) => age.TotalDays >= 1
        ? FormattableString.Invariant($"{age.Days} d {age.Hours} h")
        : age.TotalHours >= 1 ? FormattableString.Invariant($"{age.Hours} h {age.Minutes} m")
        : Math.Max(0, (int)age.TotalMinutes).ToString(CultureInfo.InvariantCulture) + " m";

    private static Button Button(FrameworkElement content, string tag, CardTheme theme)
    {
        // Own the template to avoid OS chrome, animations and per-control brushes.
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, theme.OverlayRaised);
        border.SetValue(Border.BorderBrushProperty, theme.OverlayBorder);
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
        return new Button { Content = content, Tag = tag, Template = template, MinHeight = 32, Focusable = false };
    }
}
