using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ExileSight.App.Ui;

public static partial class CardPrimitives
{
    private static readonly DoubleCollection Dashes = FreezeDashes();

    private static DoubleCollection FreezeDashes()
    {
        var dashes = new DoubleCollection { 2, 2 };
        dashes.Freeze();
        return dashes;
    }

    public static TextBlock Text(string value, CardTheme theme, string key = "OverlayForeground",
        double size = 13, bool number = false)
    {
        var text = new TextBlock
        {
            Text = value, Foreground = theme[key], FontFamily = number ? theme.NumberFont : theme.TextFont,
            FontSize = size, VerticalAlignment = VerticalAlignment.Center
        };
        Typography.SetNumeralAlignment(text, FontNumeralAlignment.Tabular);
        return text;
    }

    /// <summary>
    /// P7-T11: place pills in an Auto mod-row column; TierPillWidth keeps every row
    /// aligned and fits ench/T12 with padding. Labels remain centred within that width.
    /// </summary>
    public static FrameworkElement TierPill(TierPillModel model, CardTheme theme)
    {
        var unknown = model.Kind == PillKind.Unknown ||
            (model.Kind == PillKind.Tier && (model.Tier is null or < 1 || model.Tone == CardTone.Unknown));
        var outlined = unknown || model.Kind is PillKind.Implicit or PillKind.Enchant;
        var label = unknown ? "T?" : model.Kind switch
        {
            PillKind.Implicit => "imp", PillKind.Enchant => "ench", PillKind.Crafted => "cr",
            PillKind.Unique => "U", _ => "T" + model.Tier
        };
        var tone = model.Kind switch
        {
            PillKind.Crafted => CardTone.Crafted, PillKind.Unique => CardTone.Unique, _ => model.Tone
        };
        var grid = new Grid { Width = theme.TierPillWidth, Height = 18, HorizontalAlignment = HorizontalAlignment.Left };
        grid.Children.Add(new Rectangle
        {
            RadiusX = 3, RadiusY = 3,
            Fill = outlined ? null : theme.Tone(tone),
            Stroke = outlined ? theme[unknown ? "TooltipUnknown" : "TextMuted"] : null,
            StrokeThickness = outlined ? 1 : 0, StrokeDashArray = unknown ? Dashes : null
        });
        // §8 has no inverse-text token; reuse its darkest solid colour, AppNav,
        // for contrast on filled pills rather than introducing an unspecified token.
        var text = Text(label, theme, outlined ? "TextMuted" : "AppNav", 11, true);
        text.Margin = new Thickness(4, 0, 4, 0);
        text.FontWeight = FontWeights.SemiBold;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        grid.Children.Add(text);
        return grid;
    }

    /// <summary>Track only; pair with RollLabel in the row's separate percent column.</summary>
    public static FrameworkElement RollBar(RollBarModel model, CardTheme theme, bool stretch = false)
    {
        var position = RollPosition(model);
        var known = position.HasValue;
        var grid = new RollTrack(position) { Width = stretch ? double.NaN : 40, Height = 6, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(new Rectangle
        {
            RadiusX = 3, RadiusY = 3, Fill = known ? theme["RollTrack"] : null,
            Stroke = known ? null : theme["TooltipUnknown"], StrokeThickness = known ? 0 : 1,
            StrokeDashArray = known ? null : Dashes
        });
        if (known) grid.Children.Add(new Rectangle
        {
            Width = 40 * position!.Value, Height = 6, RadiusX = 3, RadiusY = 3,
            Fill = theme.Tone(model.Tone), HorizontalAlignment = HorizontalAlignment.Left
        });
        return grid;
    }

    private sealed class RollTrack(double? position) : Grid
    {
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            if (position.HasValue && Children.Count > 1)
                ((Rectangle)Children[1]).Width = arrangeSize.Width * position.Value;
            return base.ArrangeOverride(arrangeSize);
        }
    }

    public static TextBlock RollLabel(RollBarModel model, CardTheme theme) =>
        Text(RollPosition(model)?.ToString("0%", CultureInfo.InvariantCulture) ?? "—",
            theme, "TextSecondary", 11, true);

    private static double? RollPosition(RollBarModel model) =>
        model.Position is { } value && !double.IsNaN(value) && model.Tone != CardTone.Unknown
            ? Math.Clamp(value, 0, 1) : null;

    public static FrameworkElement SlotPips(IEnumerable<SlotState> states, CardTheme theme)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var state in states) row.Children.Add(new Rectangle
        {
            Width = 8, Height = 8, RadiusX = 2, RadiusY = 2, Margin = new Thickness(0, 0, 3, 0),
            Fill = state == SlotState.Used ? theme["TextSecondary"] : null,
            Stroke = state == SlotState.Open ? theme["OverlayAccent"] :
                state == SlotState.Unknown ? theme["TooltipUnknown"] : null,
            StrokeThickness = state == SlotState.Used ? 0 : 1,
            StrokeDashArray = state == SlotState.Unknown ? Dashes : null
        });
        return row;
    }

    public static TextBlock Delta(double value, CardTheme theme)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return Text((value > 0 ? "▲ " : value < 0 ? "▼ " : "") +
            Math.Abs(value).ToString("0.##", CultureInfo.InvariantCulture), theme,
            value > 0 ? "TooltipHigh" : value < 0 ? "TooltipLow" : "TextSecondary", 12, true);
    }
}

