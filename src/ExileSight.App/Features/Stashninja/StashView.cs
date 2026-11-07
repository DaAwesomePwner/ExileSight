using System.Net.Http;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ExileSight.App.Overlay;
using ExileSight.Core.Pricing;
using ExileSight.Core.Settings;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Stashninja;

public sealed class StashView : StackPanel
{
    private readonly StashModel model;
    private readonly IServiceProvider services;
    private readonly OverlayHost host;
    private readonly TagSurface surface;
    private readonly CalibrationFlow calibration;
    private readonly ComboBox tab = new() { IsEditable = true, Text = "currency" };
    private readonly ComboBox profile = new();
    private readonly TextBlock status = new() { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
    private readonly Button highlight = new() { IsEnabled = false, Focusable = false };
    private HostedOverlayWindow? window;
    private Layout? layout;
    private PriceOverview? overview;
    private int generation;
    public StashView(StashModel model, IServiceProvider services, FeatureViews views)
    {
        this.model = model; this.services = services;
        host = services.GetRequiredService<OverlayHost>();
        var store = new CalibrationStore(Path.Combine(services.GetRequiredService<IAppPaths>().GetGameDirectory("poe1"), "stashninja"));
        surface = new(host, services.GetRequiredService<IGameWindow>(), store);
        calibration = new(services.GetRequiredService<PassiveOverlayInput>(), services.GetRequiredService<IGameWindow>(), store);
        Background = Brushes.Black;
        Children.Add(new TextBlock { Text = "Stash prices — select tab/page", Foreground = Brushes.White });
        foreach (var l in model.Layouts) tab.Items.Add(l.TabId + "/" + l.Page);
        Children.Add(tab);
        var load = new Button { Content = "Load selected tab", Focusable = false };
        load.Click += async (_, _) => await Load(); Children.Add(load);
        foreach (var p in model.Profiles) profile.Items.Add(p.Name);
        profile.SelectedIndex = 0; profile.SelectionChanged += (_, _) => Refresh(); Children.Add(profile);
        var calibrate = new Button { Content = "Calibrate two reference cells", Focusable = false };
        calibrate.Click += (_, _) =>
        {
            if (layout is null) { status.Text = "Load a layout first"; return; }
            Invalidate();
            calibration.Start(layout, message => status.Text = message, Refresh);
        };
        Children.Add(calibrate);
        Children.Add(highlight); views.Bind(highlight, "stashninja:highlight");
        var close = new Button { Content = "Close", Focusable = false };
        close.Click += (_, _) => { generation++; calibration.Cancel(); Invalidate(); surface.Hide(); host.SetVisible(window!, false); };
        Children.Add(close); Children.Add(status);
        status.Text = store.Notice;
        tab.SelectionChanged += (_, _) => Clear();
        tab.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => Clear()));
        model.Opened += () => Dispatcher.BeginInvoke(async () => { host.SetVisible(window!, true); await Load(); });
    }
    public void Initialize()
    {
        window = (HostedOverlayWindow)Window.GetWindow(this);
        host.SetVisible(window, false);
        host.Changed += (_, _) => Position();
        window.IsVisibleChanged += (_, _) =>
        { if (!window.IsVisible) { generation++; calibration.Cancel(); Invalidate(); surface.Hide(); } };
    }
    private void Position()
    {
        if (window?.IsVisible != true) return;
        var client = services.GetRequiredService<IGameWindow>().Current.ClientRect;
        var scale = host.EffectiveScale;
        var width = Math.Min(client.Width, (int)(400 * scale));
        var height = Math.Min(client.Height, (int)(260 * scale));
        services.GetRequiredService<IOverlayWindowStyler>().Position(new WindowInteropHelper(window).Handle,
            new(client.X + client.Width - width, client.Y, width, height));
    }
    private void Invalidate() { model.SetSearch(new(null, "Load prices first")); highlight.IsEnabled = false; }
    private void Clear()
    { generation++; calibration.Cancel(); layout = null; overview = null; Invalidate(); surface.Hide(); }
    private async Task Load()
    {
        Clear(); var request = generation;
        layout = model.Layouts.FirstOrDefault(l => tab.Text == l.TabId + "/" + l.Page || tab.Text == l.TabId);
        if (layout is null)
        { status.Text = model.LayoutError ?? $"No layout yet for {tab.Text} — see data/stash-layouts README"; return; }
        var selected = layout;
        status.Text = "Loading cached prices…";
        try
        {
            var settings = services.GetRequiredService<ISettingsStore<SettingsDocument>>().Snapshot.Prices;
            var prices = services.GetRequiredService<PriceService>();
            var league = await prices.GetLeaguesAsync(settings.League);
            var results = await Task.WhenAll(selected.Cells.Select(c => c.Category).Append("Currency").Distinct()
                .Select(category => prices.GetAsync(league.DefaultLeague, category, TimeSpan.FromHours(1))));
            if (request != generation) return;
            overview = new(results.Where(r => r.Overview is not null).SelectMany(r => r.Overview!.Items).ToArray(),
                results.Select(r => r.Overview?.ChaosPerDivine).FirstOrDefault(r => r is > 0));
            Refresh();
            if (results.Any(r => r.IsStale || r.Overview is null)) status.Text += " — prices stale or incomplete";
        }
        catch (Exception e) when (e is HttpRequestException or IOException or System.Text.Json.JsonException or TaskCanceledException)
        { if (request == generation) { Invalidate(); status.Text = "Prices unavailable; try again"; } }
    }
    private void Refresh()
    {
        if (layout is null || overview is null || profile.SelectedIndex < 0) return;
        var selected = model.Profiles[profile.SelectedIndex];
        var tags = Tags.Build(layout, overview, selected);
        surface.Show(layout, tags);
        var search = Tags.Search(tags, selected.Minimum); model.SetSearch(search);
        highlight.Content = $"Highlight above {selected.Minimum}c"; highlight.IsEnabled = search.Text is not null;
        status.Text = search.Status + $" — {tags.Length}/{layout.Cells.Length} cells priced";
    }
}
