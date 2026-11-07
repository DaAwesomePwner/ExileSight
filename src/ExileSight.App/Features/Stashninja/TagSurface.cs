using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
    private readonly IOverlayWindowStyler styler;
    private readonly HostedOverlayWindow window;
    private Layout? layout;
    private PriceTag[] tags = [];
    public TagSurface(OverlayHost host, IGameWindow game, CalibrationStore store, IOverlayWindowStyler styler)
    {
        this.host = host; this.game = game; this.store = store; this.styler = styler;
        // The host needs an initial design rect; Draw replaces this owned window
        // and its surface dimensions with the current client viewport on every update.
        window = host.Register(canvas, new OverlayRect(0, 0, 1, 1), clickThrough: true);
        host.SetVisible(window, false);
        host.Changed += (_, _) => Draw();
    }
    public void Show(Layout? value, PriceTag[] prices)
    { layout = value; tags = prices; Draw(); host.SetVisible(window, value is not null); }
    public void Hide() => host.SetVisible(window, false);
    private void Draw()
    {
        canvas.Children.Clear();
        if (layout is null) return;
        var r = game.Current.ClientRect;
        if (r.Width <= 0 || r.Height <= 0) return;
        var resolution = new Resolution(r.Width, r.Height);
        var transform = store.Get(layout, resolution) ?? ExileSight.Features.Stashninja.Geometry.Fit(layout.ReferenceResolution, resolution);
        var scale = host.EffectiveScale;
        var size = ExileSight.Features.Stashninja.Geometry.Viewport(resolution, scale);
        var root = (FrameworkElement)window.Content;
        root.Width = canvas.Width = size.X; root.Height = canvas.Height = size.Y;
        if (window.IsVisible) styler.Position(new WindowInteropHelper(window).Handle, r);
        foreach (var tag in tags)
        {
            var point = transform.Apply(Layouts.Origin(layout, tag.Cell));
            var color = (Brush)new BrushConverter().ConvertFromString(tag.Color)!;
            var panel = new StackPanel { Width = tag.Cell.W * transform.Scale / scale,
                IsHitTestVisible = false, Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)) };
            panel.Children.Add(new TextBlock { Text = tag.Text, Foreground = color, FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis });
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
            Canvas.SetLeft(panel, point.X / scale); Canvas.SetTop(panel, point.Y / scale);
            canvas.Children.Add(panel);
        }
    }
}
