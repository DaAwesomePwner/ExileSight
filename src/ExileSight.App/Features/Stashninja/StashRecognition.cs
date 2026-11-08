using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Stashninja;

public sealed partial class StashView
{
    private readonly DispatcherTimer recognitionTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly CheckBox autoDetect = new() { Content = "Auto-detect taught pages", Focusable = false,
        Foreground = System.Windows.Media.Brushes.White };
    private RecognitionRegion? region;
    private PageReferenceStore? references;
    private CalibrationStore calibrationStore = null!;
    private string? candidate;
    private int matches, unknowns;
    private bool recognitionHidden, userDetectionPreference;
    private Resolution? recognitionResolution;
    private IGameWindow Game => services.GetRequiredService<IGameWindow>();
    private Resolution ClientResolution => new(Game.Current.ClientRect.Width, Game.Current.ClientRect.Height);
    private void InitializeRecognition(CalibrationStore store)
    {
        calibrationStore = store;
        string? notice = null;
        try
        {
            region = RecognitionRegion.Load(Path.Combine(AppContext.BaseDirectory, "data", "stash-layouts", "poe1", "recognition.json"), model.Layouts);
            references = new(Path.Combine(services.GetRequiredService<IAppPaths>().GetGameDirectory("poe1"), "stashninja"), region.Fingerprint);
            notice = references.Notice;
            autoDetect.IsChecked = references.Get(ClientResolution).Count > 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { notice = "Page recognition data unavailable or invalid."; autoDetect.IsEnabled = false; }
        Children.Add(autoDetect);
        autoDetect.Click += (_, _) => userDetectionPreference = true;
        autoDetect.Checked += (_, _) => { ResetRecognition(); UpdatePolling(); };
        autoDetect.Unchecked += (_, _) => { ResetRecognition(); recognitionHidden = false; UpdatePolling(); Refresh(); };
        var remember = new Button { Content = "Remember this page", Focusable = false };
        remember.Click += (_, _) => TeachPage(); Children.Add(remember);
        var forget = new Button { Content = "Forget this page", Focusable = false };
        forget.Click += (_, _) =>
        {
            if (layout is null || references is null) return;
            try { references.Forget(ClientResolution, layout.TabId + "/" + layout.Page); ResetRecognition(); SetStatus("Page reference forgotten"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { SetStatus("Could not forget page reference"); }
        };
        Children.Add(forget);
        if (notice is not null) Children.Add(new TextBlock { Text = notice, Foreground = System.Windows.Media.Brushes.Orange, TextWrapping = TextWrapping.Wrap });
        recognitionTimer.Tick += async (_, _) => await Recognize();
        host.Changed += (_, _) => UpdatePolling();
    }
    private void ActivateRecognition()
    {
        if (!userDetectionPreference) autoDetect.IsChecked = references?.Get(ClientResolution).Count > 0;
        ResetRecognition();
    }
    private void ResetRecognition() { candidate = null; matches = unknowns = 0; }
    private void UpdatePolling()
    {
        if (window?.IsVisible == true && window.RequestedVisible && Game.Current.IsForeground && !Game.Current.IsMinimized && !calibration.IsActive && autoDetect.IsChecked == true && region is not null)
            recognitionTimer.Start();
        else { recognitionTimer.Stop(); ResetRecognition(); }
    }
    private double[]? SamplePage()
    {
        var state = Game.Current;
        if (window?.IsVisible != true || !window.RequestedVisible || !state.IsForeground || state.IsMinimized || calibration.IsActive || region is null) return null;
        var resolution = ClientResolution;
        if (resolution.Width <= 0 || resolution.Height <= 0) return null;
        var b = region.Map(resolution);
        var rect = new PixelRect((int)Math.Ceiling(b.X), (int)Math.Ceiling(b.Y), (int)Math.Floor(b.Width) - 1, (int)Math.Floor(b.Height) - 1);
        static bool Overlaps(PixelRect a, Geometry.Bounds b) => a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;
        var menu = Position(state.ClientRect, host.EffectiveScale);
        if (Overlaps(rect, new(menu.X - state.ClientRect.X, menu.Y - state.ClientRect.Y, menu.Width, menu.Height))) return null;
        // Calibration can move tags into the header. Refuse capture rather than learn our own overlay.
        foreach (var l in model.Layouts)
        {
            var fit = calibrationStore.Get(l, resolution) ?? Geometry.Fit(l.ReferenceResolution, resolution);
            if (l.Cells.Any(c => Overlaps(rect, Geometry.TagBounds(l, c, fit)))) return null;
        }
        var sample = services.GetRequiredService<IScreenSampler>().TrySample(rect);
        return sample is null || sample.Width < PageSignature.Columns || sample.Height < PageSignature.Rows
            ? null : PageSignature.Create(sample.Width, sample.Height, sample.Bgra);
    }
    private void TeachPage()
    {
        if (layout is null || references is null) { SetStatus("Load a page before teaching"); return; }
        var sample = SamplePage();
        if (sample is null) { SetStatus("Cannot capture header: check foreground, resolution and overlay overlap"); return; }
        try
        {
            references.Save(ClientResolution, layout.TabId + "/" + layout.Page, sample);
            autoDetect.IsChecked = true; ResetRecognition(); UpdatePolling(); SetStatus("Page remembered locally");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { SetStatus("Could not save page reference"); }
    }
    private async Task Recognize()
    {
        UpdatePolling();
        if (!recognitionTimer.IsEnabled || references is null) return;
        if (recognitionResolution != ClientResolution) { recognitionResolution = ClientResolution; ResetRecognition(); }
        var sample = SamplePage();
        var match = sample is null ? null : PageSignature.Match(sample, references.Get(ClientResolution)
            .Where(p => model.Layouts.Any(l => l.TabId + "/" + l.Page == p.Key)).ToDictionary(p => p.Key, p => p.Value));
        if (match is null)
        {
            candidate = null; matches = 0;
            if (++unknowns >= 3) { recognitionHidden = true; surface.Hide(); Invalidate(); }
            return;
        }
        unknowns = 0;
        matches = candidate == match ? matches + 1 : 1; candidate = match;
        if (matches < 2) return;
        var selected = model.Layouts.FirstOrDefault(l => l.TabId + "/" + l.Page == match);
        if (selected is null) return;
        recognitionHidden = false;
        if (selected == layout && overview is not null) { Refresh(); return; }
        if (selected == layout) return; // Existing asynchronous load will apply the latest visibility state.
        tab.SelectedItem = match;
        await Load();
    }
}
