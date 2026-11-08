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
    private readonly RecognitionState recognition = new();
    private bool recognitionHidden { get => recognition.Hidden; set => recognition.Hidden = value; }
    private bool userDetectionPreference;
    private string? beforeCaptureNotice, captureNotice;
    private Resolution? recognitionResolution;
    private IGameWindow Game => services.GetRequiredService<IGameWindow>();
    private Resolution ClientResolution => new(Game.Current.ClientRect.Width, Game.Current.ClientRect.Height);
    private void InitializeRecognition()
    {
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
    private void ResetRecognition() => recognition.Reset();
    private void UpdatePolling()
    {
        if (window?.IsVisible == true && window.RequestedVisible && Game.Current.IsForeground && !Game.Current.IsMinimized && !calibration.IsActive && autoDetect.IsChecked == true && region is not null)
            recognitionTimer.Start();
        else { recognitionTimer.Stop(); ResetRecognition(); }
    }
    private async Task Recognize()
    {
        UpdatePolling();
        if (!recognitionTimer.IsEnabled || references is null) return;
        if (recognitionResolution != ClientResolution) { recognitionResolution = ClientResolution; ResetRecognition(); }
        var sample = SampleRegion();
        if (sample is null) { CaptureBlocked(); return; }
        var taught = references.Get(ClientResolution)
            .Where(p => model.Layouts.Any(l => l.TabId + "/" + l.Page == p.Key)).ToDictionary(p => p.Key, p => p.Value);
        var matchedTab = PageMatcher.MatchTab(sample, taught);
        string? match = null;
        if (matchedTab is not null)
        {
            var siblings = model.Layouts.Count(l => l.TabId == matchedTab) > 1;
            double[]? subSample = null;
            if (siblings)
            {
                if (!region!.SubTabs.TryGetValue(matchedTab, out var sub)) { CaptureBlocked(); return; }
                subSample = SampleRegion(sub, hideOverlappingTags: true);
                if (subSample is null) { CaptureBlocked(); return; }
            }
            match = PageMatcher.MatchPage(matchedTab, subSample, taught, siblings);
        }
        ClearCaptureNotice();
        var action = recognition.Observe(match, layout is null ? null : layout.TabId + "/" + layout.Page);
        if (action == RecognitionAction.Hide) { surface.Hide(); Invalidate(); return; }
        if (action == RecognitionAction.Restore) { Refresh(); return; }
        if (action != RecognitionAction.Select) return;
        tab.SelectedItem = match;
        await Load();
    }
    private void ClearCaptureNotice()
    {
        if (captureNotice is not null && status.Text == captureNotice) status.Text = beforeCaptureNotice ?? "";
        beforeCaptureNotice = captureNotice = null;
    }
    private void CaptureBlocked()
    {
        recognition.Observe(null, null, captureSucceeded: false);
        if (status.Text != captureNotice) beforeCaptureNotice = status.Text;
        SetStatus("Page not recognised — header blocked");
        captureNotice = status.Text;
    }
}
