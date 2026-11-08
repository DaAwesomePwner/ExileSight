using System.IO;
using System.Windows.Controls;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Stashninja;

public sealed partial class StashView
{
    private CalibrationStore calibrationStore = null!;
    private bool autoCalibrating;
    private int autoAttempt = -1;
    private volatile int autoEpoch;
    private Resolution? autoAttemptResolution;
    private GameWindowState? autoInitial;

    private void InitializeAutoCalibration()
    {
        var button = new Button { Content = "Auto-calibrate", Focusable = false };
        button.Click += async (_, _) => await AutoCalibrate(explicitRequest: true);
        Children.Add(button);
        host.Changed += async (_, _) =>
        {
            if (autoInitial is not null && !GridCapture.Current(Game.Current, autoInitial)) autoEpoch++;
            await AutoCalibrate(explicitRequest: false);
        };
    }

    private async Task AutoCalibrate(bool explicitRequest)
    {
        if (autoCalibrating || calibration.IsActive || layout is null || window?.IsVisible != true ||
            !window.RequestedVisible || !Game.Current.IsForeground || Game.Current.IsMinimized) return;
        var selected = layout; var initial = Game.Current; var resolution = ClientResolution;
        var saved = calibrationStore.Get(selected, resolution);
        if (!explicitRequest && ((autoAttempt == generation && autoAttemptResolution == resolution) || saved is not null)) return;
        autoAttempt = generation; autoAttemptResolution = resolution; autoCalibrating = true; autoInitial = initial;
        var request = generation; var epoch = autoEpoch;
        bool Current() => request == generation && epoch == autoEpoch && selected == layout &&
            !calibration.IsActive && window.IsVisible && window.RequestedVisible && GridCapture.Current(Game.Current, initial);
        UpdatePolling();
        try
        {
            var start = saved ?? Geometry.Fit(selected.ReferenceResolution, resolution);
            var area = GridCapture.Bounds(selected, resolution, start);
            var menu = Position(initial.ClientRect, host.EffectiveScale);
            if (RecognitionRegion.Intersects(new(area.X, area.Y, area.Width, area.Height),
                new(menu.X - initial.ClientRect.X, menu.Y - initial.ClientRect.Y, menu.Width, menu.Height)))
            { SetStatus("Grid capture blocked by panel; use manual calibration"); return; }
            var sampler = services.GetRequiredService<IScreenSampler>();
            GridSample? sample;
            surface.SetCaptureHidden(true);
            try
            {
                // Let WPF and the compositor remove our tags before GDI captures.
                await Task.Delay(32);
                if (!Current()) return;
                sample = await Task.Run(() => GridCapture.Sample(sampler, Game, initial, area, () => request == generation && epoch == autoEpoch));
            }
            finally { surface.SetCaptureHidden(false); }
            if (!Current()) return;
            if (sample is null) { SetStatus("Grid capture unavailable; use manual calibration"); return; }
            SetStatus("Locating grid…");
            var result = await Task.Run(() => GridLocator.Locate(selected, resolution, start, sample));
            if (!Current()) return;
            if (result.Transform is null) { SetStatus(result.Reason + " — try two empty cells"); return; }
            calibrationStore.Save(selected, resolution, result.Transform);
            Refresh();
            SetStatus("Auto-calibration saved for this page and resolution");
            TeachPage(explicitTeach: false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { if (Current()) SetStatus("Auto-calibration unavailable; use manual calibration"); }
        finally
        {
            autoInitial = null; autoCalibrating = false; UpdatePolling();
            if (request != generation) _ = Dispatcher.BeginInvoke(async () => await AutoCalibrate(explicitRequest: false));
        }
    }
}
