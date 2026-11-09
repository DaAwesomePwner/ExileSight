using System.Net.Http;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExileSight.App.Overlay;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Stashninja;

public sealed partial class StashView : StackPanel
{
    private readonly StashModel model;
    private readonly IServiceProvider services;
    private readonly OverlayHost host;
    private readonly TagSurface surface;
    private readonly CalibrationFlow calibration;
    private readonly ListBox tab = ChoiceList();
    private readonly ListBox profile = ChoiceList();
    private readonly ListBox firstCell = ChoiceList("Label");
    private readonly ListBox secondCell = ChoiceList("Label");
    private static ListBox ChoiceList(string displayMember = "")
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(FocusableProperty, false));
        // ListBoxItem normally selects through keyboard focus. Select explicitly
        // on the local mouse gesture so the game remains foreground.
        style.Setters.Add(new EventSetter(PreviewMouseLeftButtonDownEvent,
            new System.Windows.Input.MouseButtonEventHandler((sender, e) =>
            { ((ListBoxItem)sender).IsSelected = true; e.Handled = true; })));
        return new() { Height = 64, Focusable = false, DisplayMemberPath = displayMember, ItemContainerStyle = style };
    }
    private sealed record CellChoice(Cell Cell, string Label);
    private readonly TextBlock status = new() { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
    private readonly Button highlight = new() { IsEnabled = false, Focusable = false };
    private HostedOverlayWindow? window;
    private Layout? layout;
    private PriceOverview? overview;
    private bool pricesIncomplete;
    private DateTimeOffset? pricesFetchedAt;
    private volatile int generation;
    private DateTimeOffset freshUntil;
    private string? loadedLeagueSetting;
    private string? LeagueSetting => services.GetRequiredService<ISettingsStore<SettingsDocument>>().Snapshot.Prices.League;
    private void SetStatus(string message) => status.Text =
        (layout is null ? message : $"{layout.TabId}/{layout.Page}: {message}") +
        (autoDetect.IsChecked == true && automaticStatus.Length > 0 ? $" — {automaticStatus}" : "");
    public StashView(StashModel model, IServiceProvider services, FeatureViews views)
    {
        this.model = model; this.services = services;
        host = services.GetRequiredService<OverlayHost>();
        var store = new CalibrationStore(Path.Combine(services.GetRequiredService<IAppPaths>().GetGameDirectory("poe1"), "stashninja"));
        calibrationStore = store;
        surface = new(host, services.GetRequiredService<IGameWindow>(), store);
        calibration = new(services.GetRequiredService<PassiveOverlayInput>(), services.GetRequiredService<IGameWindow>(), store);
        InitializeRecognition();
        Background = Brushes.Black;
        Children.Add(new TextBlock { Text = "Stash prices — select tab/page", Foreground = Brushes.White });
        foreach (var l in model.Layouts) tab.Items.Add(l.TabId + "/" + l.Page);
        Children.Add(tab);
        var load = new Button { Content = "Load selected tab", Focusable = false };
        load.Click += async (_, _) => await LoadManual(); Children.Add(load);
        foreach (var p in model.Profiles) profile.Items.Add(p.Name);
        profile.SelectedIndex = 0; profile.SelectionChanged += (_, _) => { calibration.Cancel(); Refresh(); }; Children.Add(profile);
        Children.Add(new TextBlock { Text = "Choose two empty calibration cells (clicks reach game):", Foreground = Brushes.White });
        Children.Add(firstCell); Children.Add(secondCell);
        var calibrate = new Button { Content = "Calibrate two reference cells", Focusable = false };
        calibrate.Click += (_, _) =>
        {
            if (layout is null) { status.Text = "Load a layout first"; return; }
            if (firstCell.SelectedItem is not CellChoice first || secondCell.SelectedItem is not CellChoice second ||
                first.Cell.Id == second.Cell.Id)
            { status.Text = "Choose two distinct empty cells before calibrating"; return; }
            autoEpoch++; calibration.Cancel(); Invalidate();
            calibration.Start(layout, first.Cell, second.Cell, SetStatus, () => { Refresh(); UpdatePolling(); }, () => TeachPage(explicitTeach: false));
            UpdatePolling();
        };
        Children.Add(calibrate);
        InitializeAutoCalibration();
        Children.Add(highlight); views.Bind(highlight, "stashninja:highlight");
        var close = new Button { Content = "Close", Focusable = false };
        close.Click += (_, _) =>
        {
            calibration.Cancel();
            if (autoDetect.IsChecked != true) { generation++; Invalidate(); surface.Hide(); }
            host.SetVisible(window!, false); UpdatePolling();
        };
        Children.Add(close); Children.Add(status);
        if (model.ProfileNotice is { } notice)
            Children.Add(new TextBlock { Text = notice, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap });
        status.Text = store.Notice;
        tab.SelectionChanged += (_, _) => Clear();
        model.Opened += () =>
        {
            var selected = automaticPage ?? model.SelectedLayout;
            Dispatcher.BeginInvoke(async () =>
            {
                var page = selected is null ? null : selected.TabId + "/" + selected.Page;
                if (tab.SelectedItem as string != page) tab.SelectedItem = page;
                if (tab.SelectedItem is { } choice) tab.ScrollIntoView(choice);
                var reuse = selected is not null && selected == layout && overview is not null &&
                    services.GetRequiredService<IClock>().UtcNow < freshUntil && loadedLeagueSetting == LeagueSetting;
                if (!window!.RequestedVisible) host.SetVisible(window, true);
                ActivateRecognition();
                var loading = reuse ? Task.CompletedTask : Load();
                if (reuse) Refresh();
                UpdatePolling();
                await loading;
            });
        };
    }
    public void Initialize()
    {
        window = (HostedOverlayWindow)Window.GetWindow(this);
        window.PhysicalPlacement = Position;
        host.SetVisible(window, false);
        UpdatePolling();
        window.IsVisibleChanged += (_, _) =>
        { if (!window.IsVisible) { autoEpoch++; calibration.Cancel(); } UpdatePolling(); };
    }
    private PixelRect Position(PixelRect client, double scale)
    {
        var width = Math.Min(client.Width, (int)(400 * scale));
        var height = Math.Min(client.Height, (int)(560 * scale));
        var root = (FrameworkElement)window!.Content;
        root.Width = width / scale; root.Height = height / scale;
        return new(client.X + client.Width - width, client.Y, width, height);
    }
    private void Invalidate() { model.SetSearch(new(null, "Load prices first")); highlight.IsEnabled = false; }
    private void Clear()
    { generation++; freshUntil = default; pricesIncomplete = false; layout = null; overview = null; calibration.Cancel(); firstCell.ItemsSource = secondCell.ItemsSource = null; Invalidate(); surface.Clear(); }
    private async Task Load()
    {
        Clear(); var request = generation;
        layout = model.Layouts.FirstOrDefault(l => tab.SelectedItem as string == l.TabId + "/" + l.Page);
        if (layout is null)
        { status.Text = model.LayoutError ?? $"No layout yet for {tab.SelectedItem} — see data/stash-layouts README"; return; }
        model.RememberLoaded(layout);
        var choices = layout.Cells.Select(c => new CellChoice(c, $"{c.ItemName ?? c.NinjaId} ({c.Id})")).ToArray();
        firstCell.ItemsSource = choices; secondCell.ItemsSource = choices;
        var selected = layout;
        SetStatus("Loading cached prices…");
        try
        {
            var configuredLeague = LeagueSetting;
            var prices = services.GetRequiredService<PriceService>();
            var league = await prices.GetLeaguesAsync(configuredLeague);
            var results = await Task.WhenAll(selected.Cells.Select(c => c.Category).Append("Currency").Distinct()
                .Select(category => prices.GetAsync(league.DefaultLeague, category, TimeSpan.FromHours(1))));
            if (request != generation) return;
            overview = new(results.Where(r => r.Overview is not null).SelectMany(r => r.Overview!.Items).ToArray(),
                results.Select(r => r.Overview?.ChaosPerDivine).FirstOrDefault(r => r is > 0));
            pricesFetchedAt = results.Where(r => r.Overview is not null).Any(r => r.UpdatedAt is null)
                ? null : results.Where(r => r.Overview is not null).Select(r => r.UpdatedAt).Min();
            loadedLeagueSetting = configuredLeague;
            freshUntil = results.Any(r => r.IsStale || r.Overview is null || r.UpdatedAt is null)
                ? default : results.Min(r => r.UpdatedAt!.Value).AddHours(1);
            pricesIncomplete = results.Any(r => r.IsStale || r.Overview is null);
            Refresh();
        }
        catch (Exception e) when (e is HttpRequestException or IOException or System.Text.Json.JsonException or TaskCanceledException)
        { if (request == generation) { Invalidate(); SetStatus("Prices unavailable; try again"); } }
        if (request == generation) await AutoCalibrate(explicitRequest: false);
    }
    private void Refresh()
    {
        if (calibration.IsActive || layout is null || overview is null || profile.SelectedIndex < 0) return;
        var selected = model.Profiles[profile.SelectedIndex];
        var tags = Tags.Build(layout, overview, selected);
        if (autoDetect.IsChecked == true ? automaticPage == layout && !recognitionHidden :
            window?.RequestedVisible == true) surface.Show(layout, tags, pricesFetchedAt, services.GetRequiredService<IClock>().UtcNow);
        var search = Tags.Search(tags, selected.Minimum); model.SetSearch(search);
        highlight.Content = $"Highlight above {selected.Minimum}c"; highlight.IsEnabled = search.Text is not null;
        SetStatus(search.Status + $" — {tags.Length}/{layout.Cells.Length} cells priced" +
            (pricesIncomplete ? " — prices stale or incomplete" : ""));
    }
}
