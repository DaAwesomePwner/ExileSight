using System.Collections.Frozen;
using System.Windows;
using System.Windows.Media;

namespace ExileSight.App.Ui;

/// <summary>Immutable tokens loaded once without Application.Current or live services.</summary>
public sealed class CardTheme
{
    private static readonly Lazy<CardTheme> Shared = new(() => FromDictionary(new ResourceDictionary
    {
        Source = new Uri("/ExileSight.App;component/Ui/Tokens.xaml", UriKind.Relative)
    }));
    private readonly FrozenDictionary<string, SolidColorBrush> brushes;
    public static CardTheme Default => Shared.Value;
    public FontFamily TextFont { get; }
    public FontFamily NumberFont { get; }
    public double CardWidth => 300;
    public double TierPillWidth => 36;
    public Thickness CardPadding => new(12, 10, 12, 10);
    public double BodySize => 13;
    public double LabelSize => 11;
    public double NumberSize => 11;
    public double TitleSize => 13;
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

