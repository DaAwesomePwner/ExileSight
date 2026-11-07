// ExileSight-authored presentation. Passive log events never dispatch game commands.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ExileSight.App.Overlay;
using ExileSight.Core.Overlay;
using ExileSight.Features.Events;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.Features.Events;

public sealed class EventsView
{
    private readonly EventsModel model;
    private readonly OverlayHost host;
    private readonly ILogger<EventsView> logger;
    private readonly HostedOverlayWindow toast;
    private readonly TextBlock name = new() { Foreground = Brushes.White, FontSize = 20, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock category = new() { Foreground = Brushes.LightGray, FontSize = 13 };
    private readonly DispatcherTimer ticker = new() { Interval = TimeSpan.FromSeconds(1) };
    private Window? settings;
    private StackPanel? choices;
    private string? optionsSignature;
    private bool visible, closed;

    public EventsView(EventsModel model, OverlayHost host, ILogger<EventsView> logger)
    {
        this.model = model; this.host = host; this.logger = logger;
        var content = new StackPanel();
        content.Children.Add(category); content.Children.Add(name);
        toast = host.Register(new Border { Background = Brushes.Black, Padding = new Thickness(12), Child = content },
            // Below Timers' toast (440,80,300,100), to the right of the fixed
            // generic feature panel (20,20,400,300) and Timers panels (20,340,400,530).
            DefaultOverlayLayout.EventsToast, clickThrough: true);
        host.SetVisible(toast, false);
        model.SettingsRequested += OpenSettings;
        toast.Closed += (_, _) =>
        {
            closed = true; ticker.Stop(); model.SettingsRequested -= OpenSettings; CloseSettings();
        };
        ticker.Tick += (_, _) => Guard(Tick); ticker.Start();
    }

    private void Tick()
    {
        var current = model.Current;
        // Poll slowly when empty; retain smooth expiry while a toast is active.
        ticker.Interval = current is null ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(100);
        if (current is null && !visible && settings is null) return;
        name.Text = current?.DisplayName ?? "";
        category.Text = current?.Category ?? "";
        if (visible != (current is not null))
        { visible = current is not null; host.SetVisible(toast, visible); }
        RefreshSettings();
    }

    private void OpenSettings()
    {
        if (!toast.Dispatcher.CheckAccess())
        { toast.Dispatcher.BeginInvoke(OpenSettings); return; }
        Guard(OpenSettingsCore);
    }

    private void OpenSettingsCore()
    {
        if (closed) return;
        if (settings is not null) { settings.Activate(); return; }
        choices = new StackPanel { Margin = new Thickness(16) };
        settings = new Window
        {
            Title = "Map-event notifications", Width = 400, Height = 300,
            Content = new ScrollViewer { Content = choices }, ResizeMode = ResizeMode.CanResize
        };
        settings.Closed += (_, _) => ClearSettingsReferences();
        RefreshSettings();
        settings.Show(); // Only the explicitly invoked local settings command opens this window.
    }

    private void Guard(Action action) => OverlayFailureGuard.Run(action, () =>
    {
        visible = false;
        try { host.SetVisible(toast, false); }
        finally { CloseSettings(); }
    }, error => logger.LogWarning("Map-event overlay failed: {ExceptionType}", error.GetType().Name));

    private void ClearSettingsReferences()
    { settings = null; choices = null; optionsSignature = null; }

    private void CloseSettings()
    {
        var failed = settings;
        // Clear before cleanup, including when Show() threw, so a later open never activates a failed window.
        ClearSettingsReferences();
        failed?.Close();
    }

    private void RefreshSettings()
    {
        if (choices is null) return;
        var options = model.Options;
        var signature = string.Join("\n", options.Select(o => $"{o.Event.Id}|{o.Event.DisplayName}|{o.Event.Category}|{o.Enabled}"));
        if (optionsSignature == signature) return;
        optionsSignature = signature;
        choices.Children.Clear();
        choices.Children.Add(new TextBlock
        {
            Text = options.Length == 0 ? "Event data unavailable." : "Choose which map events show a short notification.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
        });
        foreach (var option in options)
        {
            var check = new CheckBox
            {
                Content = option.Event.DisplayName + " · " + option.Event.Category,
                IsChecked = option.Enabled, Margin = new Thickness(0, 4, 0, 4)
            };
            void Save()
            {
                try { model.SetEnabled(option.Event.Id, check.IsChecked == true); }
                catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
                { check.IsChecked = option.Enabled; check.ToolTip = "Could not save: " + error.GetType().Name; }
            }
            check.Click += (_, _) => Save();
            choices.Children.Add(check);
        }
    }
}
