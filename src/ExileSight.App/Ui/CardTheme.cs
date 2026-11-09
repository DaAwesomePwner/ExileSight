using System.Collections.Frozen;
using ExileSight.Core.Settings;
using System.Windows;
using System.Windows.Media;

namespace ExileSight.App.Ui;

/// <summary>Immutable tokens loaded once without Application.Current or live services.</summary>
public sealed class CardTheme
{
    private static readonly object Sync = new();
    private static CardTheme? shared;
    private readonly FrozenDictionary<string, SolidColorBrush> brushes;
    public static CardTheme Default
    {
        get
        {
            lock (Sync)
            {
                // LoadComponent initializes WPF's application resource package without an
                // Application instance. A bare ResourceDictionary.Source can run before it.
                // Publish only on success so a failed load can be retried by a later caller.
                return shared ??= FromDictionary((ResourceDictionary)Application.LoadComponent(
                    new Uri("/ExileSight.App;component/Ui/Tokens.xaml", UriKind.Relative)));
            }
        }
    }
    public FontFamily TextFont { get; }
    public FontFamily NumberFont { get; }
    public double RowHeight { get; private init; } = 28;
    private static CardTheme[]? variants;
    public static CardTheme For(OverlayDensity density, OverlayContrast contrast)
    {
        lock (Sync)
        {
            variants ??= [Create(OverlayDensity.Comfortable, false), Create(OverlayDensity.Compact, false),
                Create(OverlayDensity.Comfortable, true), Create(OverlayDensity.Compact, true)];
            return variants[(density == OverlayDensity.Compact ? 1 : 0) +
                (contrast == OverlayContrast.HighContrast ? 2 : 0)];
        }
    }
    private static CardTheme Create(OverlayDensity density, bool highContrast)
    {
        var source = Default;
        var values = source.brushes.ToDictionary();
        if (highContrast)
        {
            values["TextSecondary"] = source.OverlayForeground;
            values["TextMuted"] = source.TextSecondary;
            values["OverlayRule"] = source.OverlayBorder;
            values["OverlayBorder"] = source.TooltipUnknown;
        }
        return new(values, source.TextFont, source.NumberFont)
        { RowHeight = density == OverlayDensity.Compact ? 24 : 28 };
    }
    public double CardWidth => 300;
    public double TierPillWidth => 36;
    public Thickness CardPadding => new(12, 10, 12, 10);
    public double BodySize => 12;
    public double LabelSize => 10;
    public double NumberSize => 10;
    public double TitleSize => 12;
    // Larger emphasis sizes are separate from the compact overlay text ramp.
    public double GuideStepSize => 14;
    public double StatValueSize => 20;
    public double AppTitleSize => 24;
    public double AppSectionSize => 18;
    public double AppBodySize => 14;
    public SolidColorBrush this[string key] => brushes[key];
    public SolidColorBrush OverlayForeground => this["OverlayForeground"];
    public SolidColorBrush OverlayBackground => this["OverlayBackground"];
    public SolidColorBrush OverlayAccent => this["OverlayAccent"];
    public SolidColorBrush TooltipBest => this["TooltipBest"];
    public SolidColorBrush TooltipHigh => this["TooltipHigh"];
    public SolidColorBrush TooltipMedium => this["TooltipMedium"];
    public SolidColorBrush TooltipLow => this["TooltipLow"];
    public SolidColorBrush TooltipCrafted => this["TooltipCrafted"];
    public SolidColorBrush TooltipUnique => this["TooltipUnique"];
    public SolidColorBrush TooltipUnknown => this["TooltipUnknown"];
    public SolidColorBrush OverlayRaised => this["OverlayRaised"];
    public SolidColorBrush OverlayBorder => this["OverlayBorder"];
    public SolidColorBrush OverlayRule => this["OverlayRule"];
    public SolidColorBrush TextSecondary => this["TextSecondary"];
    public SolidColorBrush TextMuted => this["TextMuted"];
    public SolidColorBrush TextFaint => this["TextFaint"];
    public SolidColorBrush Danger1 => this["Danger1"];
    public SolidColorBrush Danger2 => this["Danger2"];
    public SolidColorBrush Danger3 => this["Danger3"];
    public SolidColorBrush Accent => this["Accent"];
    public SolidColorBrush AppBackground => this["AppBackground"];
    public SolidColorBrush AppPanel => this["AppPanel"];
    public SolidColorBrush AppNav => this["AppNav"];
    public SolidColorBrush StatTileBackground => this["StatTileBackground"];
    public SolidColorBrush RollTrack => this["RollTrack"];
    public SolidColorBrush DangerRowBackground => this["DangerRowBackground"];
    public SolidColorBrush Tone(CardTone tone) => this["Tooltip" + tone];

    private CardTheme(Dictionary<string, SolidColorBrush> values, FontFamily text, FontFamily numbers)
    {
        brushes = values.ToFrozenDictionary();
        TextFont = text;
        NumberFont = numbers;
    }

    public static CardTheme FromDictionary(ResourceDictionary dictionary)
    {
        var values = new Dictionary<string, SolidColorBrush>();
        Collect(dictionary, values);
        return new CardTheme(values, (FontFamily)dictionary["OverlayFont"], (FontFamily)dictionary["NumberFont"]);
    }

    private static void Collect(ResourceDictionary dictionary, Dictionary<string, SolidColorBrush> values)
    {
        foreach (var merged in dictionary.MergedDictionaries) Collect(merged, values);
        foreach (var key in dictionary.Keys.OfType<string>())
        {
            if (dictionary[key] is not SolidColorBrush source) continue;
            var brush = source.IsFrozen ? source : source.Clone();
            if (!brush.IsFrozen) brush.Freeze();
            values[key] = brush;
        }
    }
}

