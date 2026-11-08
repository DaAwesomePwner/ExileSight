using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ExileSight.App.Features.Vaalasync;
using ExileSight.App.Ui;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using ExileSight.Core.Status;
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
    private bool narrow, refreshing, pending, closed;
    private ShellModel? displayed;
    private HistoryView? history;
    private readonly ExileSight.Features.Guide.GuidePageState guidePageState = new();
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private ComboBox? LeagueBox => ShellViewState.Find<ComboBox>(Content as DependencyObject).FirstOrDefault();
    public ShellWindow(IServiceProvider services)
    {
        this.services = services; source = new(services);
        Title = "ExileSight"; Width = 1280; Height = 820; MinWidth = 520; MinHeight = 480;
        Background = CardTheme.Default.AppBackground;
        Activated += (_, _) => Refresh();
        refreshTimer.Tick += (_, _) => { if (page is "Home" or "Act guide") Refresh(); };
        IsVisibleChanged += (_, _) => UpdateTimer();
        StateChanged += (_, _) => UpdateTimer();
        Closed += (_, _) => { closed = true; refreshTimer.Stop(); };
        SizeChanged += (_, _) => { if (narrow != (ActualWidth < 760)) Refresh(); };
        AddHandler(Button.ClickEvent, new RoutedEventHandler(Clicked));
        Refresh();
    }
    private void UpdateTimer()
    {
        if (IsVisible && WindowState != WindowState.Minimized) refreshTimer.Start(); else refreshTimer.Stop();
    }
    private bool CanReplace(bool changed, bool layoutChanged) => ShellRefreshPolicy.ShouldRebuild(changed, layoutChanged,
        LeagueBox?.IsKeyboardFocusWithin == true || LeagueBox?.IsDropDownOpen == true, LeagueBox?.Text, displayed?.League);
    private async void Refresh()
    {
        if (closed || !CanReplace(true, true)) return;
        if (refreshing) { pending = true; return; }
        refreshing = true;
        try
        {
            do
            {
                pending = false;
                var requestedPage = page;
                var next = await source.ReadAsync(requestedPage);
                if (closed) return;
                if (requestedPage != page) { pending = true; continue; }
                if (requestedPage == "Act guide")
                    next = next with { Guide = source.Model<ExileSight.Features.Guide.GuideModel>()?.StructuredSnapshot() };
                var nextNarrow = ActualWidth > 0 && ActualWidth < 760;
                // Recheck after the await: the user may have started editing during the read.
                if (!CanReplace(displayed is null || !displayed.SameContent(next), narrow != nextNarrow)) continue;
                var offsets = displayed?.Page == next.Page ? ShellViewState.Capture(Content as DependencyObject) : [];
                narrow = nextNarrow; displayed = next;
                var view = ShellBuilder.Build(next, CardTheme.Default, narrow, next.Page == "Act guide"
                    ? new Features.Guide.GuidePageHost(source.Model<ExileSight.Features.Guide.GuideModel>(), guidePageState) : null);
                Content = view;
                _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    if (ReferenceEquals(Content, view)) ShellViewState.Restore(view, offsets);
                }));
                Title = "ExileSight";
            } while (pending);
        }
        catch (Exception error)
        {
            // Retain the existing view and edits on transient storage failures. No modal timer dialogs.
            Title = "ExileSight — status unavailable (" + error.GetType().Name + ")";
        }
        finally { refreshing = false; }
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
        if (target == "DiscardLeague")
        {
            if (LeagueBox is { } box) box.Text = displayed?.League ?? "";
            Refresh(); return;
        }
        if (target == "SaveLeague")
        {
            var value = LeagueBox?.Text.Trim();
            if (string.IsNullOrEmpty(value)) return;
            try
            {
                services.GetRequiredService<ISettingsStore<SettingsDocument>>().Update("prices", d => d.Prices.League = value);
                if (displayed is not null) displayed = displayed with { League = value };
                if (LeagueBox is { } box) box.Text = value;
                Refresh();
            }
            catch (Exception error) { MessageBox.Show("Could not save league: " + error.GetType().Name, "ExileSight"); }
            return;
        }
        if (!CanReplace(true, true))
        {
            MessageBox.Show("Save or discard the league edit before changing pages.", "ExileSight"); return;
        }
        page = target; Refresh();
    }
}
