using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ExileSight.App.Ui;
using ExileSight.Core.Overlay;
using ExileSight.Core.Settings;
using ExileSight.Features.MapInfo;
using ExileSight.Platform.Abstractions.Storage;
namespace ExileSight.App.Shell.Pages;

public sealed class OverlayPageHost : ContentControl
{
    private readonly ISettingsStore<SettingsDocument> store;
    private readonly double aspect;
    public bool Dirty { get; private set; }
    public OverlayPageHost(ISettingsStore<SettingsDocument> store, double aspect)
    {
        this.store = store; this.aspect = aspect; Reload();
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => Dirty = true));
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, _) => Dirty = true));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(Changed));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(Changed));
        AddHandler(OverlayPreview.PositionChangedEvent, new RoutedEventHandler((_, _) => Dirty = true));
        AddHandler(Button.ClickEvent, new RoutedEventHandler(Clicked));
    }
    private T Find<T>(string name) where T : FrameworkElement => ShellViewState.Find<T>(Content as DependencyObject).First(e => e.Name == name);
    private void Changed(object sender, RoutedEventArgs args)
    {
        Dirty = true;
        Find<Canvas>("DockPreview").Visibility = Find<RadioButton>("Docked").IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        Find<TextBox>("Scale").IsEnabled = Find<CheckBox>("AutoScale").IsChecked != true;
    }
    private void Reload()
    {
        var document = store.Snapshot;
        Content = OverlayPageBuilder.Build(new(document.Overlay, MapInfoSettings.Load(new OverlaySettingsContext(document, "mapinfo")), aspect), CardTheme.Default);
        Find<TextBox>("Scale").IsEnabled = document.Overlay.ScaleOverride is not null;
        Dirty = false;
    }
    private void Clicked(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is not Button { Tag: string action } || action is not ("SaveOverlay" or "DiscardOverlay")) return;
        args.Handled = true;
        if (action == "DiscardOverlay") { Reload(); return; }
        try
        {
            decimal Threshold(string name) => decimal.Parse(Find<TextBox>(name).Text, CultureInfo.CurrentCulture);
            var thresholds = new MapInfoSettings(Threshold("Quantity"), Threshold("Rarity"), Threshold("PackSize")).Validate();
            double? scale = Find<CheckBox>("AutoScale").IsChecked == true ? null : double.Parse(Find<TextBox>("Scale").Text, CultureInfo.CurrentCulture);
            var position = (Point)Find<Canvas>("DockPreview").Tag;
            var preferences = new OverlayPreferences(
                Find<RadioButton>("Docked").IsChecked == true ? CardPlacementMode.Docked : CardPlacementMode.BesideCursor,
                position.X, position.Y, scale, (OverlayDensity)Find<ComboBox>("Density").SelectedIndex,
                (OverlayContrast)Find<ComboBox>("Contrast").SelectedIndex);
            store.Update("overlay", document =>
            {
                preferences.Apply(document.Overlay);
                thresholds.Save(new OverlaySettingsContext(document, "mapinfo"));
            });
            Dirty = false; Find<TextBlock>("OverlayNotice").Text = "Changes saved.";
        }
        catch (Exception error) when (error is FormatException or OverflowException or ArgumentException or System.IO.IOException or InvalidOperationException)
        { Find<TextBlock>("OverlayNotice").Text = "Not saved. Use a scale of 0.8–1.5 and nonnegative percentages. " + error.GetType().Name; }
    }
}
