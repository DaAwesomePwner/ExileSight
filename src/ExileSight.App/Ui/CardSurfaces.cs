using System.Windows;
using System.Windows.Controls;

namespace ExileSight.App.Ui;

public static partial class CardPrimitives
{
    public static Border CardFrame(FrameworkElement content, CardTheme theme) => new()
    {
        Width = theme.CardWidth, Padding = theme.CardPadding, CornerRadius = new CornerRadius(6),
        Background = theme["OverlayBackground"], BorderBrush = theme["OverlayBorder"],
        BorderThickness = new Thickness(1), Child = content, UseLayoutRounding = true
    };

    public static Border Hairline(CardTheme theme) => new()
    {
        Height = 1, Background = theme["OverlayRule"], Margin = new Thickness(0, 8, 0, 8)
    };

    public static Border VerdictBand(VerdictModel model, CardTheme theme)
    {
        if (model.Wanted < 0 || model.Unwanted < 0) throw new ArgumentOutOfRangeException(nameof(model));
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (model.Wanted > 0)
        {
            var wanted = Text($"◆ {model.Wanted} wanted", theme, "OverlayAccent", theme.BodySize);
            wanted.FontWeight = FontWeights.SemiBold;
            row.Children.Add(wanted);
        }
        if (model.Unwanted > 0)
        {
            var unwanted = Text($"✕ {model.Unwanted} unwanted", theme, "TooltipLow", theme.BodySize);
            unwanted.FontWeight = FontWeights.SemiBold;
            unwanted.Margin = new Thickness(model.Wanted > 0 ? 8 : 0, 0, 0, 0);
            row.Children.Add(unwanted);
        }
        return new Border
        {
            Background = theme["OverlayRaised"], CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(8, 6, 8, 6), Child = row,
            Visibility = model.Wanted + model.Unwanted == 0 ? Visibility.Collapsed : Visibility.Visible
        };
    }

    public static Border VerdictBand(UniqueVerdictModel model, CardTheme theme)
    {
        var roll = new RollBarModel(model.AverageRoll, CardTone.High);
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(Text("Average roll", theme, "TextSecondary", theme.BodySize));
        var bar = RollBar(roll, theme, stretch: true);
        bar.Margin = new Thickness(12, 0, 8, 0);
        Grid.SetColumn(bar, 1);
        row.Children.Add(bar);
        var number = RollLabel(roll, theme);
        number.FontWeight = FontWeights.Bold;
        number.FontSize = theme.NumberSize;
        number.Foreground = theme.OverlayForeground;
        number.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(number, 2);
        row.Children.Add(number);
        return new Border
        {
            Background = theme.OverlayRaised, CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Top, Padding = new Thickness(8, 6, 8, 6),
            Child = row
        };
    }

    public static Border StatTile(StatTileModel model, CardTheme theme)
    {
        var column = new StackPanel();
        column.Children.Add(Text(model.Label, theme, "TextMuted", theme.LabelSize));
        column.Children.Add(Text(model.Value ?? "?", theme,
            model.Value is null || model.Tone is null or CardTone.Unknown
                ? "OverlayForeground" : "Tooltip" + model.Tone, theme.StatValueSize, true));
        return new Border
        {
            Background = theme["OverlayRaised"], CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8), Child = column
        };
    }
}

