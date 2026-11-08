using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.Core.Overlay;
using static ExileSight.App.Shell.ShellElements;
namespace ExileSight.App.Shell.Pages;

public static class OverlayPageBuilder
{
    public static FrameworkElement Build(OverlayPageModel model, CardTheme t)
    {
        var root = new StackPanel();
        root.Children.Add(Text("Overlay", t, 24, true));
        root.Children.Add(Text("Where cards appear and how big they are. Save changes to apply them.", t, muted: true));
        var columns = new WrapPanel();
        var settings = new StackPanel { Width = 460 };
        settings.Children.Add(Text("ITEM AND MAP CARD POSITION", t, 12, true, true));
        foreach (var (name, label, detail, selected) in new[] {
            ("BesideCursor", "Beside the cursor, away from the game tooltip", "Opens right of the cursor over your inventory and left of it over your stash. Closes when you move the mouse.", model.Settings.PlacementMode == CardPlacementMode.BesideCursor),
            ("Docked", "Docked in one spot", "Drag the sample card on the preview of your game window.", model.Settings.PlacementMode == CardPlacementMode.Docked) })
        {
            var body = new StackPanel(); body.Children.Add(Text(label, t, 14, true)); body.Children.Add(Text(detail, t, muted: true));
            var option = new RadioButton { Name = name, GroupName = "Placement", Content = body, IsChecked = selected,
                Foreground = t.OverlayForeground, Padding = new Thickness(10) };
            var frame = Card(option, t);
            void Highlight() { frame.BorderBrush = option.IsChecked == true ? t.Accent : t.OverlayBorder; }
            option.Checked += (_, _) => Highlight(); option.Unchecked += (_, _) => Highlight(); Highlight();
            settings.Children.Add(frame);
        }
        var preview = OverlayPreview.Create(model, t);
        preview.Visibility = model.Settings.PlacementMode == CardPlacementMode.Docked ? Visibility.Visible : Visibility.Collapsed;
        settings.Children.Add(preview);
        settings.Children.Add(Text("Overlay size · applies to every overlay", t, 14, true));
        settings.Children.Add(new CheckBox { Name = "AutoScale", Content = "Auto · scale with game client height", IsChecked = model.Settings.ScaleOverride is null,
            Foreground = t.OverlayForeground, Margin = new Thickness(0, 4, 0, 12) });
        settings.Children.Add(Text("Fixed scale (0.8–1.5)", t));
        var scale = Field("Scale", (model.Settings.ScaleOverride ?? 1).ToString(CultureInfo.CurrentCulture), t);
        scale.IsEnabled = model.Settings.ScaleOverride is not null;
        settings.Children.Add(scale);
        settings.Children.Add(Text("Density", t, 14, true));
        settings.Children.Add(Choices("Density", ["Comfortable", "Compact"], (int)model.Settings.Density));
        settings.Children.Add(Text("Colours", t, 14, true));
        settings.Children.Add(Choices("Contrast", ["Standard", "High contrast"], (int)model.Settings.Contrast));
        columns.Children.Add(Card(settings, t));
        var thresholds = new StackPanel { Width = 290 };
        thresholds.Children.Add(Text("MAP CARD HIGHLIGHTS", t, 12, true, true));
        thresholds.Children.Add(Text("Numbers turn teal at or above these values.", t, muted: true));
        foreach (var (name, label, value) in new[] { ("Quantity", "Quantity %", model.Thresholds.QuantityThreshold),
            ("Rarity", "Rarity %", model.Thresholds.RarityThreshold), ("PackSize", "Pack size %", model.Thresholds.PackSizeThreshold) })
        {
            thresholds.Children.Add(Text(label, t));
            thresholds.Children.Add(Field(name, value.ToString(CultureInfo.CurrentCulture), t));
        }
        columns.Children.Add(Card(thresholds, t)); root.Children.Add(columns);
        columns.SizeChanged += (_, _) =>
        {
            settings.Width = Math.Min(460, Math.Max(160, columns.ActualWidth - 52));
            thresholds.Width = Math.Min(290, Math.Max(160, columns.ActualWidth - 52));
            preview.Width = Math.Min(430, settings.Width);
            preview.Height = preview.Width / model.AspectRatio;
        };
        var buttons = new WrapPanel();
        buttons.Children.Add(Button("Save changes", "SaveOverlay", t)); buttons.Children.Add(Button("Discard changes", "DiscardOverlay", t));
        root.Children.Add(buttons);
        root.Children.Add(new TextBlock { Name = "OverlayNotice", Foreground = t.TooltipLow, TextWrapping = TextWrapping.Wrap });
        return root;
    }
    private static TextBox Field(string name, string text, CardTheme t) => new() { Name = name, Text = text, Width = 120,
        Background = t.AppNav, Foreground = t.OverlayForeground, BorderBrush = t.OverlayBorder, Padding = new Thickness(8),
        HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16), FontFamily = t.NumberFont };
    private static ComboBox Choices(string name, string[] values, int index) => new() { Name = name, ItemsSource = values,
        SelectedIndex = index, Margin = new Thickness(0, 0, 0, 16), HorizontalAlignment = HorizontalAlignment.Left, Width = 240 };
}
