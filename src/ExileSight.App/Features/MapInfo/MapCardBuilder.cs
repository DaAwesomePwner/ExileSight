using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ExileSight.App.Ui;
using ExileSight.Features.MapInfo;

namespace ExileSight.App.Features.MapInfo;

/// <summary>Service-free card. Hosts handle tagged row buttons; folding is local presentation state.</summary>
public static class MapCardBuilder
{
    private static readonly ControlTemplate RowButtonTemplate = MakeButtonTemplate();
    private static ControlTemplate MakeButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        template.Seal();
        return template;
    }
    private static readonly DoubleCollection Dashes = MakeDashes();
    private static DoubleCollection MakeDashes()
    { var value = new DoubleCollection { 2, 2 }; value.Freeze(); return value; }

    public static FrameworkElement Build(MapCardModel model, CardTheme theme)
    {
        var panel = new StackPanel();
        var title = new Grid();
        title.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var name = Text(model.Result.Name, theme); name.FontWeight = FontWeights.SemiBold;
        title.Children.Add(name);
        var metadata = Text(model.Metadata, theme, "TextMuted", 10);
        metadata.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(metadata, 1); title.Children.Add(metadata);
        panel.Children.Add(title);
        var tiles = new Grid { Margin = new Thickness(0, 8, 0, 10) };
        AddTile(tiles, "Quantity", model.Result.Quantity, model.QuantityGood, theme);
        AddTile(tiles, "Rarity", model.Result.Rarity, model.RarityGood, theme);
        AddTile(tiles, "Pack size", model.Result.PackSize, model.PackSizeGood, theme);
        panel.Children.Add(tiles);
        if (model.ShowFirstTimeHint)
        {
            var hint = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            hint.Children.Add(new Rectangle { Stroke = theme.TooltipUnknown, StrokeThickness = 1,
                StrokeDashArray = Dashes, RadiusX = 3, RadiusY = 3 });
            var text = Text("Rate each mod once by clicking it. Every later map sorts itself.", theme, "TextSecondary", 12);
            text.Margin = new Thickness(8); hint.Children.Add(text); panel.Children.Add(hint);
        }
        else
        {
            var band = new WrapPanel();
            band.Children.Add(Text($"{model.Deadly} deadly  ", theme, "Danger3", 12));
            band.Children.Add(Text($"{model.Risky} risky  ", theme, "Danger2", 12));
            band.Children.Add(Text("for your ratings", theme, "TextMuted", 12));
            panel.Children.Add(new Border { Background = model.Deadly > 0 ? theme.DangerRowBackground : theme.OverlayRaised,
                CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 8), Child = band });
        }
        foreach (var row in model.VisibleRows) panel.Children.Add(Row(row, theme));
        var folded = model.FoldedRows.ToArray();
        if (folded.Length > 0)
        {
            var reveal = FoldButton(folded.Length, theme);
            var hidden = new StackPanel { Visibility = Visibility.Collapsed };
            foreach (var row in folded) hidden.Children.Add(Row(row, theme));
            reveal.Click += (_, _) => { reveal.Visibility = Visibility.Collapsed; hidden.Visibility = Visibility.Visible; };
            panel.Children.Add(reveal); panel.Children.Add(hidden);
        }
        if (model.Notice is { } notice) panel.Children.Add(Text(notice, theme, "TooltipLow", 11));
        var footer = Text("Click a mod to cycle 0–3 · rating saved for every map", theme, "TextFaint", 11);
        footer.Margin = new Thickness(0, 10, 0, 0); panel.Children.Add(footer);
        return CardPrimitives.CardFrame(panel, theme);
    }

    private static void AddTile(Grid tiles, string label, decimal? value, bool good, CardTheme theme)
    {
        var column = tiles.ColumnDefinitions.Count; tiles.ColumnDefinitions.Add(new());
        var display = value is { } number ? number.ToString("0.##", CultureInfo.InvariantCulture) + "%" : null;
        var content = new StackPanel();
        content.Children.Add(CardPrimitives.Text(display ?? "?", theme,
            good ? "OverlayAccent" : "OverlayForeground", 20, number: true));
        content.Children.Add(CardPrimitives.Text(label, theme, "TextMuted", 11));
        var tile = new Border { Background = theme.StatTileBackground, CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8), Child = content };
        tile.Margin = new Thickness(column == 0 ? 0 : 6, 0, 0, 0);
        Grid.SetColumn(tile, column); tiles.Children.Add(tile);
    }

    private static FrameworkElement Row(MapRow row, CardTheme theme)
    {
        var grid = new Grid { MinHeight = 30, Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(34) });
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(78) });
        grid.ColumnDefinitions.Add(new());
        if (!row.IsUnknown && row.Rank == 3) grid.Background = theme.DangerRowBackground;
        var pips = row.IsUnknown ? Text("?", theme, "TooltipLow") : Pips(row.Rank, theme);
        pips.Margin = new Thickness(4, 0, 0, 0); grid.Children.Add(pips);
        var area = Text(MapCardModel.AreaLabel(row), theme, "TextSecondary", 11);
        Grid.SetColumn(area, 1); grid.Children.Add(area);
        var label = Text(row.IsUnknown ? "Not in map data · check in game" : row.Text, theme);
        label.Margin = new Thickness(8, 2, 0, 2); Grid.SetColumn(label, 2); grid.Children.Add(label);
        if (row.IsUnknown) return grid;
        var button = Button(grid); button.Tag = row.Id; return button;
    }

    private static FrameworkElement Pips(int? rank, CardTheme theme)
    {
        var group = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        // 4 px inset + 3 × (6 px pip + 2 px gap) leaves 6 px before the area column.
        for (var i = 0; i < 3; i++) group.Children.Add(new Ellipse
        {
            Width = 6, Height = 6, Margin = new Thickness(0, 0, 2, 0),
            Fill = i < rank ? theme["Danger" + rank] : null,
            Stroke = i < rank ? null : theme.TooltipUnknown, StrokeThickness = 1,
            StrokeDashArray = rank is null ? Dashes : null
        });
        return group;
    }

    private static Button FoldButton(int count, CardTheme theme)
    {
        var grid = new Grid { MinHeight = 30 };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(34) });
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(78) });
        grid.ColumnDefinitions.Add(new());
        var pips = Pips(null, theme); pips.Margin = new Thickness(4, 0, 0, 0); grid.Children.Add(pips);
        var countLabel = Text($"+{count} more", theme, "TextMuted", 11);
        Grid.SetColumn(countLabel, 1); grid.Children.Add(countLabel);
        var label = Text("rated 0 · click to show", theme, "TextMuted");
        label.Margin = new Thickness(8, 2, 0, 2); Grid.SetColumn(label, 2); grid.Children.Add(label);
        return Button(grid);
    }

    private static TextBlock Text(string value, CardTheme theme, string key = "OverlayForeground", double size = 13)
    { var text = CardPrimitives.Text(value, theme, key, size); text.TextWrapping = TextWrapping.Wrap; return text; }
    private static Button Button(FrameworkElement content)
    {
        var button = new Button { Content = content, Focusable = false, BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0) };
        // A plain presenter avoids OS button chrome, effects and hover animations.
        button.Template = RowButtonTemplate;
        return button;
    }
}
