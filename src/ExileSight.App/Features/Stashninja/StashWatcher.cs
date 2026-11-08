using System.IO;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Stashninja;

public sealed partial class StashView
{
    private Layout? automaticPage;
    private bool identifying;
    private Layout? loadingPage;
    private int automaticEpoch;
    private double[]? lastHeader;
    private DateTimeOffset lastGridRead;
    private DateTimeOffset? unknownSince;

    private async Task WatchStash()
    {
        UpdatePolling();
        if (!recognitionTimer.IsEnabled || identifying || model.Layouts.Length == 0) return;
        if (recognitionResolution != ClientResolution)
        {
            recognitionResolution = ClientResolution;
            automaticPage = null; lastHeader = null; generation++; surface.Hide();
        }
        var now = services.GetRequiredService<IClock>().UtcNow;
        var header = SampleRegion().Signature;
        var changed = header is null || lastHeader is null ||
            header.Zip(lastHeader).Average(p => Math.Abs(p.First - p.Second)) > .04;
        lastHeader = header;
        if (!changed && automaticPage is not null && now - lastGridRead < TimeSpan.FromSeconds(2.5)) return;
        identifying = true;
        var initial = Game.Current;
        var resolution = ClientResolution;
        var epoch = automaticEpoch;
        var request = generation;
        bool Current() => epoch == automaticEpoch && request == generation && !calibration.IsActive &&
            autoDetect.IsChecked == true && GridCapture.Current(Game.Current, initial);
        try
        {
            var starts = model.Layouts.ToDictionary(l => l,
                l => calibrationStore.Get(l, resolution) ?? Geometry.Fit(l.ReferenceResolution, resolution));
            var areas = model.Layouts.Select(l => GridSampling.Bounds(l, resolution, starts[l])).ToArray();
            var x = areas.Min(a => a.X); var y = areas.Min(a => a.Y);
            var area = new GridArea(x, y, areas.Max(a => a.X + a.Width) - x, areas.Max(a => a.Y + a.Height) - y);
            var menu = Position(initial.ClientRect, host.EffectiveScale);
            if (window?.IsVisible == true && RecognitionRegion.Intersects(new(area.X, area.Y, area.Width, area.Height),
                new(menu.X - initial.ClientRect.X, menu.Y - initial.ClientRect.Y, menu.Width, menu.Height)))
            { RejectPage(now); return; }
            GridSample? sample;
            var sampler = services.GetRequiredService<IScreenSampler>();
            surface.SetCaptureHidden(true);
            try
            {
                await Task.Delay(32);
                if (!Current()) return;
                sample = await Task.Run(() => GridCapture.Sample(sampler, Game, initial, area,
                    () => epoch == automaticEpoch && request == generation));
            }
            finally { surface.SetCaptureHidden(false); }
            if (!Current()) return;
            lastGridRead = now;
            var result = sample is null ? null : await Task.Run(() =>
                GridIdentifier.Identify(model.Layouts, resolution, sample, l => starts[l]));
            if (!Current()) return;
            if (result?.Layout is not { } identified || result.Transform is null)
            { RejectPage(now); return; }
            unknownSince = null;
            automaticPage = identified;
            recognitionHidden = false;
            if (calibrationStore.Get(identified, resolution) is null)
                calibrationStore.Save(identified, resolution, result.Transform);
            if (loadingPage == identified) return;
            var reuse = layout == identified && overview is not null && now < freshUntil && loadedLeagueSetting == LeagueSetting;
            if (reuse) { Refresh(); return; }
            tab.SelectedItem = identified.TabId + "/" + identified.Page;
            // Release the capture guard before waiting for prices so later ticks can hide
            // a closed stash and invalidate a pending price response.
            _ = LoadAutomatic(identified);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { if (Current()) RejectPage(now); }
        finally { identifying = false; }
    }

    private async Task LoadAutomatic(Layout identified)
    {
        loadingPage = identified;
        try { await Load(); }
        finally { if (loadingPage == identified) loadingPage = null; }
    }

    private void RejectPage(DateTimeOffset now)
    {
        if (automaticPage is not null) generation++;
        unknownSince ??= now;
        automaticPage = null;
        recognitionHidden = true;
        surface.Hide();
        if (now - unknownSince >= TimeSpan.FromSeconds(1))
        {
            generation++;
            Invalidate();
        }
    }
}
