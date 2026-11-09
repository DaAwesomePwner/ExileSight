using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Storage;
using System.Windows;
using System.Windows.Controls;
using ExileSight.App.Ui;
using ExileSight.App.Overlay;
using ExileSight.Core.Overlay;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Window;

namespace ExileSight.App.Features.Stashninja;

public sealed class TagSurface
{
    private readonly OverlayTheme overlayTheme;
    private readonly Canvas canvas = new() { IsHitTestVisible = false, ClipToBounds = true };
    private readonly OverlayHost host;
    private readonly IGameWindow game;
    private readonly CalibrationStore store;
    private readonly HostedOverlayWindow window;
    private bool captureHidden, requested;
    private Layout? layout;
    private RecognitionRegion? recognition;
    public void SetRecognitionRegion(RecognitionRegion? value)
    { recognition = value; drawn = null; Draw(); }
    private PriceTag[] tags = [];
    private (Layout Layout, ExileSight.Features.Stashninja.Transform Transform, Resolution Resolution, double Scale)? drawn;
    private PriceTag[] drawnTags = [];
    private string age = "", drawnAge = "";
    private ExileSight.Features.Stashninja.Geometry.Bounds[] drawnBounds = [];
    public TagSurface(OverlayHost host, IGameWindow game, CalibrationStore store, ISettingsStore<SettingsDocument> overlaySettings)
    {
        overlayTheme = new(overlaySettings);
        this.host = host; this.game = game; this.store = store;
        // The host owns native placement; never resize again from Changed.
        window = host.Register(canvas, DefaultOverlayLayout.StashRegistration, clickThrough: true);
        window.PhysicalPlacement = (client, _) => client;
        host.SetVisible(window, false);
        host.Changed += (_, _) => Draw();
        overlayTheme.Watch(window, () => { drawn = null; Draw(); });
    }
    public void Show(Layout? value, PriceTag[] prices, DateTimeOffset? fetchedAt, DateTimeOffset now)
    {
        layout = value; tags = prices; age = TagPresentation.Age(fetchedAt, now); Draw();
        requested = value is not null;
        UpdateVisibility();
    }
    public void Clear()
    {
        requested = false; layout = null; tags = []; drawn = null; drawnTags = []; drawnBounds = [];
        canvas.Children.Clear();
    }
    public string? VisibleTabId => window.RequestedVisible ? layout?.TabId : null;
    public bool Overlaps(ExileSight.Features.Stashninja.Geometry.Bounds region)
    {
        if (!window.RequestedVisible || layout is null) return false;
        return drawnBounds.Any(bounds => RecognitionRegion.Intersects(region, bounds));
    }
    private void UpdateVisibility()
    {
        var visible = requested && !captureHidden;
        if (window.RequestedVisible != visible) host.SetVisible(window, visible);
    }
    public void SetCaptureHidden(bool value)
    { captureHidden = value; UpdateVisibility(); }
    public void Hide()
    { requested = false; if (window.RequestedVisible) host.SetVisible(window, false); }
    private static bool SameTags(PriceTag[] a, PriceTag[] b) => a.Length == b.Length &&
        a.Zip(b).All(p => p.First.Cell == p.Second.Cell && p.First.Text == p.Second.Text &&
            p.First.Color == p.Second.Color && p.First.Name == p.Second.Name && p.First.Chaos == p.Second.Chaos &&
            p.First.Sparkline.SequenceEqual(p.Second.Sparkline));
    private void Draw()
    {
        if (layout is null) return;
        var r = game.Current.ClientRect;
        if (r.Width <= 0 || r.Height <= 0) return;
        var resolution = new Resolution(r.Width, r.Height);
        var transform = store.Get(layout, resolution) ?? ExileSight.Features.Stashninja.Geometry.Fit(layout.ReferenceResolution, resolution);
        var scale = host.EffectiveScale;
        var key = (layout, transform, resolution, scale);
        if (drawn == key && drawnAge == age && SameTags(tags, drawnTags)) return;
        drawn = key; drawnTags = tags; drawnAge = age;
        canvas.Children.Clear();
        var size = ExileSight.Features.Stashninja.Geometry.Viewport(resolution, scale);
        var root = (FrameworkElement)window.Content;
        root.Width = canvas.Width = size.X; root.Height = canvas.Height = size.Y;
        var visuals = StashTagBuilder.Build(layout, tags, transform, resolution, scale, age, overlayTheme.Current, recognition);
        drawnBounds = visuals.Select(v => v.Bounds).Select(b =>
            new ExileSight.Features.Stashninja.Geometry.Bounds(Math.Max(0, b.X), Math.Max(0, b.Y),
                Math.Max(0, Math.Min(r.Width, b.X + b.Width) - Math.Max(0, b.X)),
                Math.Max(0, Math.Min(r.Height, b.Y + b.Height) - Math.Max(0, b.Y))))
            .Where(b => b.Width > 0 && b.Height > 0).ToArray();
        foreach (var visual in visuals)
        {
            Canvas.SetLeft(visual.Element, visual.Bounds.X / scale);
            Canvas.SetTop(visual.Element, visual.Bounds.Y / scale);
            canvas.Children.Add(visual.Element);
        }
    }
}
