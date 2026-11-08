using System.IO;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Stashninja;

public sealed partial class StashView
{
    private double[]? SampleRegion(Geometry.Bounds? sub = null, bool hideOverlappingTags = false)
    {
        var state = Game.Current;
        if (window?.IsVisible != true || !window.RequestedVisible || !state.IsForeground || state.IsMinimized || calibration.IsActive || region is null) return null;
        var resolution = ClientResolution;
        if (resolution.Width <= 0 || resolution.Height <= 0) return null;
        var b = region.Map(resolution, sub);
        var rect = new PixelRect((int)Math.Ceiling(b.X), (int)Math.Ceiling(b.Y), (int)Math.Floor(b.Width) - 1, (int)Math.Floor(b.Height) - 1);
        var sampled = new Geometry.Bounds(rect.X, rect.Y, rect.Width, rect.Height);
        var menu = Position(state.ClientRect, host.EffectiveScale);
        if (RecognitionRegion.Intersects(sampled, new(menu.X - state.ClientRect.X, menu.Y - state.ClientRect.Y, menu.Width, menu.Height))) return null;
        if (surface.Overlaps(sampled))
        {
            if (hideOverlappingTags)
            {
                recognitionHidden = true; surface.Hide(); Invalidate();
                // Let WPF/composition remove the old tags before sampling on the next tick.
            }
            return null;
        }
        var sample = services.GetRequiredService<IScreenSampler>().TrySample(rect);
        return sample is null || sample.Width < PageSignature.Columns || sample.Height < PageSignature.Rows
            ? null : PageSignature.Create(sample.Width, sample.Height, sample.Bgra);
    }
    private void TeachPage(bool explicitTeach = true)
    {
        if (layout is null || references is null) { SetStatus("Load a page before teaching"); return; }
        var header = SampleRegion();
        double[]? subSignature = null;
        if (region!.SubTabs.TryGetValue(layout.TabId, out var sub))
        {
            subSignature = SampleRegion(sub, hideOverlappingTags: true);
            if (subSignature is null) { CaptureBlocked(); return; }
        }
        if (header is null) { CaptureBlocked(); return; }
        try
        {
            references.Save(ClientResolution, layout.TabId + "/" + layout.Page, header, subSignature);
            autoDetect.IsChecked = RecognitionState.EnableAfterTeach(explicitTeach, userDetectionPreference, autoDetect.IsChecked == true);
            ResetRecognition(); UpdatePolling(); SetStatus("Page remembered locally");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { SetStatus("Could not save page reference"); }
    }
}
