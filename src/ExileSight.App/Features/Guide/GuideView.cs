using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using System.Windows.Threading;
using ExileSight.App.Overlay;
using ExileSight.Core.Overlay;
using ExileSight.Features.Guide;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Features.Guide;

public sealed class GuideView
{
    private readonly GuideModel model;
    private readonly OverlayHost host;
    private readonly IGameWindow game;
    private readonly HostedOverlayWindow panel;
    private readonly Border content = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly CardTheme theme = CardTheme.Default;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer checkpoint = new() { Interval = TimeSpan.FromSeconds(60) };
    private bool saving;
    public GuideView(GuideModel model, OverlayHost host, IGameWindow game)
    {
        this.model = model; this.host = host; this.game = game;
        panel = host.Register(content, DefaultOverlayLayout.Guide, clickThrough: true);
        host.SetVisible(panel, false);
        host.Changed += FocusChanged;
        panel.IsVisibleChanged += (_, _) => timer.IsEnabled = panel.IsVisible;
        timer.Tick += (_, _) => Refresh();
        model.VisibilityChanged += Present;
        checkpoint.Tick += async (_, _) =>
        {
            if (saving) return;
            saving = true;
            try { await Task.Run(model.Checkpoint); }
            finally { saving = false; }
        };
        checkpoint.Start();
        panel.Closed += (_, _) => { timer.Stop(); checkpoint.Stop(); host.Changed -= FocusChanged; model.VisibilityChanged -= Present; };
    }
    private void Present()
    {
        if (!panel.Dispatcher.CheckAccess()) { panel.Dispatcher.BeginInvoke(Present); return; }
        Refresh();
    }
    private void FocusChanged(object? sender, EventArgs args)
    {
        if (!game.Current.IsForeground || !game.Current.Found || game.Current.IsMinimized) model.Hide();
    }
    private void Refresh()
    {
        try
        {
            if (!model.Visible) { host.SetVisible(panel, false); return; }
            var state = model.StructuredSnapshot(); content.Child = GuideCardBuilder.Build(state, theme);
            host.SetVisible(panel, state.Visible && game.Current.IsForeground && game.Current.Found && !game.Current.IsMinimized);
        }
        catch (Exception)
        { model.Hide(); try { host.SetVisible(panel, false); } catch (Exception) { panel.Hide(); } }
        finally { timer.IsEnabled = panel.IsVisible; }
    }
}
