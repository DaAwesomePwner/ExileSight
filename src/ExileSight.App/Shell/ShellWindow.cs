using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using ExileSight.App.Features.Vaalasync;
using ExileSight.App.Ui;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using ExileSight.Features.MapTracker;
using ExileSight.Features.Vaalasync;
using ExileSight.Platform.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;
namespace ExileSight.App.Shell;

public sealed class ShellWindow : Window
{
    private readonly IServiceProvider services;
    private readonly ShellSource source;
    private string page = "Home";
    private bool narrow;
    private HistoryView? history;
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    public ShellWindow(IServiceProvider services)
    {
        this.services = services; source = new(services);
        Title = "ExileSight"; Width = 1280; Height = 820; MinWidth = 520; MinHeight = 480;
        Background = CardTheme.Default.AppBackground;
        Activated += (_, _) => Refresh();
        refreshTimer.Tick += (_, _) =>
        {
            if (page == "Home" && Keyboard.FocusedElement is not TextBoxBase &&
                FindCombo((DependencyObject)Content)?.IsKeyboardFocusWithin != true) Refresh();
        };
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && WindowState != WindowState.Minimized) refreshTimer.Start(); else refreshTimer.Stop();
        };
        StateChanged += (_, _) =>
        {
            if (IsVisible && WindowState != WindowState.Minimized) refreshTimer.Start(); else refreshTimer.Stop();
        };
        Closed += (_, _) => refreshTimer.Stop();
        SizeChanged += (_, _) => { if (narrow != (ActualWidth < 760)) Refresh(); };
        AddHandler(Button.ClickEvent, new RoutedEventHandler(Clicked));
        Refresh();
    }
    private void Refresh()
    {
        narrow = ActualWidth > 0 && ActualWidth < 760;
        Content = ShellBuilder.Build(source.Read(page), CardTheme.Default, narrow);
    }
    private void Clicked(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is not Button { Tag: string target }) return;
        if (target == "Map log") { source.Model<TrackerModel>()?.Open(); return; }
        if (target == "Sales")
        {
            if (source.Model<ShopModel>() is not { } shop) return;
            if (history is null)
            {
                history = new(shop, services.GetRequiredService<PriceService>());
                history.Closed += (_, _) => history = null;
            }
            history.Show(); history.WindowState = WindowState.Normal; history.Activate(); return;
        }
        if (target == "SaveLeague")
        {
            var value = FindCombo((DependencyObject)Content)?.Text.Trim();
            if (string.IsNullOrEmpty(value)) return;
            try
            {
                services.GetRequiredService<ISettingsStore<SettingsDocument>>().Update("prices", d => d.Prices.League = value);
                Refresh();
            }
            catch (Exception error) { MessageBox.Show("Could not save league: " + error.GetType().Name, "ExileSight"); }
            return;
        }
        page = target; Refresh();
    }
    private static ComboBox? FindCombo(DependencyObject root)
    {
        if (root is ComboBox combo) return combo;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindCombo(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }
}
