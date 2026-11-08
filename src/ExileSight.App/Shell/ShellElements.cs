using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
namespace ExileSight.App.Shell;

internal static class ShellElements
{
    internal static TextBlock Text(string text, CardTheme t, double size = 14, bool bold = false, bool muted = false) => new()
    {
        Text = text, FontFamily = t.TextFont, FontSize = size, Foreground = muted ? t.TextMuted : t.OverlayForeground,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 8)
    };
    internal static Border Card(UIElement child, CardTheme t) => new()
    {
        Child = child, Padding = new Thickness(20), Background = t.AppPanel, BorderBrush = t.OverlayBorder,
        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 12, 16)
    };
    internal static Button Button(string label, string target, CardTheme t) => new()
    {
        Content = label, Tag = target, Background = t.OverlayRaised, Foreground = t.OverlayForeground,
        BorderBrush = t.OverlayBorder, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 8, 8)
    };
    internal static FrameworkElement Keys(IReadOnlyList<KeyRow> keys, CardTheme t)
    {
        var panel = new StackPanel();
        foreach (var key in keys)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var cap = new Border { Child = Text(key.Keys, t, 12, true), Background = t.OverlayRaised,
                BorderBrush = t.OverlayBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 4, 6, 0), HorizontalAlignment = HorizontalAlignment.Left };
            row.Children.Add(cap);
            var name = Text(key.Name + (key.Enabled ? "" : " · disabled"), t);
            Grid.SetColumn(name, 1); row.Children.Add(name); panel.Children.Add(row);
        }
        return panel;
    }
}
