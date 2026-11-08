using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ExileSight.App.Overlay;
using ExileSight.Core.Overlay;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Features.Stashninja;

public sealed class TagSurface
{
    private readonly Canvas canvas = new() { IsHitTestVisible = false, ClipToBounds = true };
    private readonly OverlayHost host;
    private readonly IGameWindow game;
    private readonly CalibrationStore store;
    private readonly HostedOverlayWindow window;
    private Layout? layout;
    private PriceTag[] tags = [];
    private (Layout Layout, ExileSight.Features.Stashninja.Transform Transform, Resolution Resolution, double Scale)? drawn;
    private PriceTag[] drawnTags = [];
    public TagSurface(OverlayHost host, IGameWindow game, CalibrationStore store)
    {
        this.host = host; this.game = game; this.store = store;
        // The host owns native placement; never resize again from Changed.
        window = host.Register(canvas, DefaultOverlayLayout.StashRegistration, clickThrough: true);
        window.PhysicalPlacement = (client, _) => client;
        host.SetVisible(window, false);
        host.Changed += (_, _) => Draw();
    }
    public void Show(Layout? value, PriceTag[] prices)
    {
        layout = value; tags = prices; Draw();
        if (window.RequestedVisible != (value is not null)) host.SetVisible(window, value is not null);
    }
    public void Clear()
    {
        layout = null; tags = []; drawn = null; drawnTags = [];
        canvas.Children.Clear();
    }
    public bool Overlaps(ExileSight.Features.Stashninja.Geometry.Bounds region)
    {
        if (!window.RequestedVisible || layout is null) return false;
        var client = game.Current.ClientRect;
        var resolution = new Resolution(client.Width, client.Height);
        var transform = store.Get(layout, resolution) ?? ExileSight.Features.Stashninja.Geometry.Fit(layout.ReferenceResolution, resolution);
        return tags.Any(t => RecognitionRegion.Intersects(region,
            ExileSight.Features.Stashninja.Geometry.TagBounds(layout, t.Cell, transform)));
    }
    public void Hide()
    { if (window.RequestedVisible) host.SetVisible(window, false); }
    private static bool SameTags(PriceTag[] a, PriceTag[] b) => a.Length == b.Length &&
        a.Zip(b).All(p => p.First.Cell == p.Second.Cell && p.First.Text == p.Second.Text &&
            p.First.Color == p.Second.Color && p.First.Sparkline.SequenceEqual(p.Second.Sparkline));
    private void Draw()
    {
        if (layout is null) return;
        var r = game.Current.ClientRect;
        if (r.Width <= 0 || r.Height <= 0) return;
        var resolution = new Resolution(r.Width, r.Height);
        var transform = store.Get(layout, resolution) ?? ExileSight.Features.Stashninja.Geometry.Fit(layout.ReferenceResolution, resolution);
        var scale = host.EffectiveScale;
        var key = (layout, transform, resolution, scale);
        if (drawn == key && SameTags(tags, drawnTags)) return;
        drawn = key; drawnTags = tags;
        canvas.Children.Clear();
        var size = ExileSight.Features.Stashninja.Geometry.Viewport(resolution, scale);
        var root = (FrameworkElement)window.Content;
        root.Width = canvas.Width = size.X; root.Height = canvas.Height = size.Y;
        foreach (var tag in tags)
        {
            var bounds = ExileSight.Features.Stashninja.Geometry.TagBounds(layout, tag.Cell, transform);
            var color = (Brush)new BrushConverter().ConvertFromString(tag.Color)!;
            var panel = new StackPanel { Width = bounds.Width / scale, ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false, Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)) };
            panel.Children.Add(new TextBlock { Text = tag.Text, Foreground = color, FontSize = 11,
                TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var chart = new Canvas { Height = 12, IsHitTestVisible = false };
            var values = tag.Sparkline.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
            if (values.Length > 0)
            {
                var min = values.Min(); var span = Math.Max(1, values.Max() - min);
                for (int i = 1; i < 7; i++)
                    if (tag.Sparkline[i - 1] is { } a && tag.Sparkline[i] is { } b)
                        chart.Children.Add(new Line { X1 = (i - 1) * panel.Width / 6, X2 = i * panel.Width / 6,
                            Y1 = 12 - (a - min) / span * 12, Y2 = 12 - (b - min) / span * 12, Stroke = color });
            }
            panel.Children.Add(chart);
            var frame = new Border { Width = bounds.Width / scale, Height = bounds.Height / scale,
                ClipToBounds = true, Child = panel };
            Canvas.SetLeft(frame, bounds.X / scale); Canvas.SetTop(frame, bounds.Y / scale);
            canvas.Children.Add(frame);
        }
    }
}
