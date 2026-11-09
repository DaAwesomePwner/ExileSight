using System.IO;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Stashninja;

public sealed partial class StashView
{
    private Layout? automaticPage, loadingPage;
    private bool identifying;
    private int automaticEpoch;
    private StashWatchPolicy watchPolicy = new();
    private readonly WideSearchBudget wideSearchBudget = new();
    private readonly FirstUseCalibration calibrationConfirmation = new();
    private string automaticStatus = "";

    private double[]? WatchHeader() => SampleRegion(passive: true).Signature;
    private double[]? WatchSubTab(Layout page) =>
        region!.SubTabs.TryGetValue(page.TabId, out var bounds)
            ? SampleRegion(bounds, page.TabId, passive: true).Signature : null;

    private async Task WatchStash()
    {
        UpdatePolling();
        if (!recognitionTimer.IsEnabled || identifying || model.Layouts.Length == 0) return;
        if (recognitionResolution != ClientResolution)
        {
            recognitionResolution = ClientResolution;
            watchPolicy = new(); calibrationConfirmation.Reset(); automaticPage = null; generation++; surface.Hide();
        }
        var now = services.GetRequiredService<IClock>().UtcNow;
        var header = WatchHeader();
        wideSearchBudget.ObserveHeader(header);
        var sub = automaticPage is null ? null : WatchSubTab(automaticPage);
        var step = watchPolicy.Tick(now, header, sub);
        if (step.Work == WatchWork.None) return;
        if (step.Work == WatchWork.Unknown) { RejectPage(now, false, header); return; }
        identifying = true;
        var initial = Game.Current; var resolution = ClientResolution;
        var epoch = automaticEpoch; var request = generation;
        bool Current() => epoch == automaticEpoch && request == generation && !calibration.IsActive &&
            autoDetect.IsChecked == true && GridCapture.Current(Game.Current, initial);
        try
        {
            var saved = model.Layouts.ToDictionary(l => l, l => calibrationStore.Get(l, resolution) ?? calibrationConfirmation.Pending(l, resolution));
            var starts = model.Layouts.ToDictionary(l => l, l => saved[l] ?? Geometry.Fit(l.ReferenceResolution, resolution));
            var sampler = services.GetRequiredService<IScreenSampler>();
            Dictionary<Layout, double>? precheck = null;
            if (step.Work == WatchWork.Precheck)
            {
                precheck = await Task.Run(() => GridPrecheck.Score(model.Layouts, resolution, l => saved[l],
                    area => GridCapture.Sample(sampler, Game, initial, area,
                        () => epoch == automaticEpoch && request == generation)));
                if (!Current()) return;
                if (!precheck.Values.Any(StashWatchPolicy.ShouldIdentify))
                { RejectPage(now, step.Changed, header); return; }
            }
            var areas = model.Layouts.Select(l => GridSampling.Bounds(l, resolution, starts[l])).ToArray();
            var x = areas.Min(a => a.X); var y = Math.Min(areas.Min(a => a.Y), (int)(94 * resolution.Height / 837d));
            var area = new GridArea(x, y, areas.Max(a => a.X + a.Width) - x, areas.Max(a => a.Y + a.Height) - y);
            var menu = Position(initial.ClientRect, host.EffectiveScale);
            if (window?.IsVisible == true && RecognitionRegion.Intersects(new(area.X, area.Y, area.Width, area.Height),
                new(menu.X - initial.ClientRect.X, menu.Y - initial.ClientRect.Y, menu.Width, menu.Height)))
            { RejectPage(now, step.Changed, header); return; }
            // Only a changed baseline or a qualifying sparse pre-check reaches this capture.
            surface.SetCaptureHidden(true);
            try
            {
                await Task.Delay(32); // Let the compositor remove our tag panels before GDI capture.
                if (!Current()) return;
                var baselineHeader = WatchHeader();
                var baselineSub = model.Layouts.GroupBy(l => l.TabId).ToDictionary(g => g.Key, g => WatchSubTab(g.First()));
                var sample = await Task.Run(() => GridCapture.Sample(sampler, Game, initial, area,
                    () => epoch == automaticEpoch && request == generation));
                if (!Current()) return;
                // Search runs after restoring tags; its cost is not hidden time.
                surface.SetCaptureHidden(false);
                var result = sample is null ? null : await Task.Run(() =>
                    GridIdentifier.Identify(model.Layouts, resolution, sample, l => automaticPage is null ? saved[l] : starts[l], precheck,
                        candidates => wideSearchBudget.Reserve(now, baselineHeader, candidates), currencyButtons, displayedPage: automaticPage, tagScale: host.EffectiveScale));
                if (!Current()) return;
                if (result?.Layout is not { } identified || result.Transform is null || baselineHeader is null ||
                    (region!.SubTabs.ContainsKey(identified.TabId) && baselineSub[identified.TabId] is null))
                {
                    if (automaticPage is { } displayed && result?.HoverPage == displayed)
                    {
                        watchPolicy.Retain(now, true, header, sub, adoptBaseline: false);
                        return;
                    }
                    RejectPage(now, step.Changed, header);
                    return;
                }
                if (calibrationStore.Get(identified, resolution) is null)
                {
                    if (!calibrationConfirmation.Observe(identified, resolution, result.Transform))
                    {
                        // Keep only a transient start transform for the next identification;
                        // otherwise the wide-search cooldown would prevent confirmation.
                        if (watchPolicy.Unknown(now, true, baselineHeader))
                        {
                            generation++; Invalidate();
                            // This transition invalidates the old page, not our pending fit.
                            request = generation;
                        }
                        automaticPage = null; recognitionHidden = true; surface.Hide();
                        SetStatus("Confirming grid calibration…");
                        return;
                    }
                    calibrationStore.Save(identified, resolution, result.Transform);
                }
                calibrationConfirmation.Reset();
                watchPolicy.Accept(baselineHeader, baselineSub[identified.TabId]);
                automaticPage = identified; recognitionHidden = false;
                automaticStatus = $"auto: {identified.TabId}/{identified.Page} (score {result.Score:F0}, margin {result.Margin:F0})";
                if (loadingPage == identified) return;
                var reuse = layout == identified && overview is not null && now < freshUntil && loadedLeagueSetting == LeagueSetting;
                if (reuse) { Refresh(); return; }
                tab.SelectedItem = identified.TabId + "/" + identified.Page;
                _ = LoadAutomatic(identified);
            }
            finally { surface.SetCaptureHidden(false); }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { if (Current()) RejectPage(now, step.Changed, header); }
        finally { if (!Current()) calibrationConfirmation.Reset(); identifying = false; }
    }

    private async Task LoadAutomatic(Layout identified)
    {
        loadingPage = identified;
        try { await Load(); }
        finally { if (loadingPage == identified) loadingPage = null; }
    }

    private void RejectPage(DateTimeOffset now, bool changed, double[]? header)
    {
        calibrationConfirmation.Reset();
        if (!(automaticPage is not null
            ? watchPolicy.Retain(now, false, header, null)
            : watchPolicy.Unknown(now, changed, header))) return;
        generation++; Invalidate();
        automaticPage = null; recognitionHidden = true;
        automaticStatus = "auto: unknown";
        surface.Hide();
        SetStatus("Page not identified");
    }

    private async Task LoadManual()
    {
        var selected = model.Layouts.FirstOrDefault(l => tab.SelectedItem as string == l.TabId + "/" + l.Page);
        if (autoDetect.IsChecked == true && selected is not null && WatchHeader() is { } header)
        {
            automaticEpoch++;
            watchPolicy.Accept(header, WatchSubTab(selected), pinned: true);
            automaticPage = selected; recognitionHidden = false;
            automaticStatus = $"manual: {selected.TabId}/{selected.Page} (pinned until header changes)";
        }
        await Load();
    }
}
