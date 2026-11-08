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
    private readonly DispatcherTimer recognitionTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly CheckBox autoDetect = new() { Content = "Show stash prices automatically", Focusable = false,
        Foreground = System.Windows.Media.Brushes.White };
    private RecognitionRegion? region;
    private PageReferenceStore? references;
    private readonly RecognitionState recognition = new();
    private bool recognitionHidden { get => recognition.Hidden; set => recognition.Hidden = value; }
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
            autoDetect.IsChecked = model.AutomaticPrices;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { notice = "Page recognition data unavailable or invalid."; autoDetect.IsEnabled = false; }
        Children.Add(autoDetect);
        autoDetect.Click += (_, _) =>
        {
            model.SetAutomaticPrices(autoDetect.IsChecked == true);
            automaticPage = null; surface.Hide(); UpdatePolling();
        };
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
        recognitionTimer.Tick += async (_, _) => await WatchStash();
        host.Changed += (_, _) => UpdatePolling();
    }
    private void ActivateRecognition()
    {
        autoDetect.IsChecked = model.AutomaticPrices;
        ResetRecognition();
    }
    private void ResetRecognition() => recognition.Reset();
    private void UpdatePolling()
    {
        if (window is not null && Game.Current.Found && Game.Current.IsForeground && !Game.Current.IsMinimized && !calibration.IsActive && !autoCalibrating && autoDetect.IsChecked == true && region is not null)
            recognitionTimer.Start();
        else
        {
            recognitionTimer.Stop(); automaticEpoch++; automaticPage = null;
            if (autoDetect.IsChecked == true) surface.Hide();
            ResetRecognition();
        }
    }
    private void ClearCaptureNotice()
    {
        if (captureNotice is not null && status.Text == captureNotice) status.Text = beforeCaptureNotice ?? "";
        beforeCaptureNotice = captureNotice = null;
    }
    private void CaptureBlocked(string? message = null)
    {
        recognition.Observe(null, null, captureSucceeded: false);
        ShowRecognitionNotice(message ?? "Page not recognised — header blocked");
    }
    private void ShowRecognitionNotice(string message)
    {
        if (status.Text != captureNotice) beforeCaptureNotice = status.Text;
        SetStatus(message);
        captureNotice = status.Text;
    }
}
