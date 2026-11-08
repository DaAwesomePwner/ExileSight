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
    public Thickness CardPadding => new(12, 10, 12, 10);
    public double BodySize => 13;
    public double LabelSize => 11;
    public double NumberSize => 11;
    public double TitleSize => 13;
    public double AppTitleSize => 24;
    public double AppSectionSize => 18;
    public double AppBodySize => 14;
    public SolidColorBrush this[string key] => brushes[key];
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

