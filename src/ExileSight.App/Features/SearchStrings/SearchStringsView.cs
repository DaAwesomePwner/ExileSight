using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExileSight.App.Overlay;
using ExileSight.Core.Input;
using ExileSight.Features.SearchStrings;
using Microsoft.Extensions.Logging;

namespace ExileSight.App.Features.SearchStrings;

/// <summary>Hosted NOACTIVATE menu; only FeatureViews.Bind can redeem a physical click.</summary>
public sealed class SearchStringsView : ScrollViewer
{
    private readonly SearchMenu model;
    private readonly FeatureViews views;
    private readonly OverlayHost host;
    private readonly PassiveOverlayInput passive;
    private readonly ILogger<SearchStringsView> logger;
    private readonly StackPanel panel = new();
    private HostedOverlayWindow? window;
    private bool submitting;
    public SearchStringsView(SearchMenu model, FeatureViews views, OverlayHost host,
        PassiveOverlayInput passive, ILogger<SearchStringsView> logger)
    {
        this.model = model; this.views = views; this.host = host; this.passive = passive; this.logger = logger;
        Background = Brushes.Black; Content = panel;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        model.Opened += Open;
    }
    public void Initialize()
    {
        window = (HostedOverlayWindow)Window.GetWindow(this);
        host.SetVisible(window, false);
        window.IsVisibleChanged += VisibilityChanged;
        window.Closed += (_, _) => { model.Opened -= Open; passive.Close(this); model.Cancel(); };
    }
    private void Open() => Dispatcher.BeginInvoke(() => Guard(() =>
    {
        Vendors();
        host.SetVisible(window!, true);
    }));
    private void Guard(Action action) => OverlayFailureGuard.Run(action,
        () => { model.Cancel(); passive.Close(this); host.SetVisible(window!, false); },
        error => logger.LogWarning("Search menu failed: {ExceptionType}", error.GetType().Name));
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (window!.IsVisible) passive.Open(this, Observed);
        else { passive.Close(this); if (!submitting) model.Cancel(); }
    }
    private void Header(string text) => panel.Children.Add(new TextBlock
    { Text = text, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) });
    private static Button Button(string text) => new()
    {
        Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
        Focusable = false, Margin = new Thickness(3), Padding = new Thickness(5)
    };
    private void Vendors()
    {
        panel.Children.Clear();
        Header("Search strings — pick the vendor / UI by hand");
        if (model.Notice is { } notice) Header(notice);
        foreach (var vendor in model.Vendors)
        {
            var button = Button(vendor);
            button.Click += (_, _) => Guard(() => { model.ChooseVendor(vendor); Searches(); });
            panel.Children.Add(button);
        }
        CloseButton();
    }
    private void Searches()
    {
        panel.Children.Clear();
        Header(model.Vendor + " — click to highlight; wheel over a search to cycle lines");
        var back = Button("Back to vendors");
        back.Click += (_, _) => Guard(() => { model.Cancel(); Vendors(); });
        panel.Children.Add(back);
        for (var i = 0; i < model.Entries.Count; i++)
        {
            var entry = i;
            string Label() => model.Entries[entry].Name + "\n" + model.Text(entry);
            var button = Button(Label());
            button.PreviewMouseWheel += (_, e) => Guard(() =>
            {
                model.Cycle(entry, e.Delta > 0 ? -1 : 1);
                ((TextBlock)button.Content).Text = Label(); e.Handled = true;
            });
            // Prepare before Bind's mouse-up handler; never use Click to mint a token.
            button.PreviewMouseLeftButtonUp += (_, _) =>
            { if (button.IsMouseOver && button.IsEnabled) model.Prepare(entry); };
            panel.Children.Add(button);
            views.Bind(button, "searchstr:paste");
            button.Click += (_, _) => Guard(() =>
            {
                submitting = true;
                try { host.SetVisible(window!, false); }
                finally { submitting = false; }
            });
        }
        CloseButton();
    }
    private void CloseButton()
    {
        var close = Button("Close");
        close.Click += (_, _) => Guard(Close);
        panel.Children.Add(close);
    }
    private void Close() { model.Cancel(); host.SetVisible(window!, false); }
    private void Observed(PassiveInput input)
    {
        if (window?.IsVisible != true) return;
        Guard(() =>
        {
            var start = window.PointToScreen(new Point());
            var end = window.PointToScreen(new Point(window.ActualWidth, window.ActualHeight));
            var inside = input.X >= start.X && input.X < end.X && input.Y >= start.Y && input.Y < end.Y;
            if (input.Message == 0x100 || (input.Message is 0x201 or 0x204 or 0x207 or 0x20b && !inside)) Close();
        });
    }
}
