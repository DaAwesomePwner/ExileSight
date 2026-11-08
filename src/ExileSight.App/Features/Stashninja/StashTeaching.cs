using System.IO;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;

namespace ExileSight.App.Features.Stashninja;

public sealed partial class StashView
{
    private (double[]? Signature, string? Notice) SampleRegion(Geometry.Bounds? sub = null, string? matchedTab = null, bool passive = false)
    {
        var state = Game.Current;
        if (!state.IsForeground || state.IsMinimized || calibration.IsActive || region is null) return (null, null);
        var resolution = ClientResolution;
        if (resolution.Width <= 0 || resolution.Height <= 0) return (null, null);
        var b = region.Map(resolution, sub);
        var rect = new PixelRect((int)Math.Ceiling(b.X), (int)Math.Ceiling(b.Y), (int)Math.Floor(b.Width) - 1, (int)Math.Floor(b.Height) - 1);
        var sampled = new Geometry.Bounds(rect.X, rect.Y, rect.Width, rect.Height);
        var menu = Position(state.ClientRect, host.EffectiveScale);
        if (window?.IsVisible == true && RecognitionRegion.Intersects(sampled, new(menu.X - state.ClientRect.X, menu.Y - state.ClientRect.Y, menu.Width, menu.Height))) return (null, null);
        var decision = SubTabCapture.Decide(surface.Overlaps(sampled), surface.VisibleTabId, matchedTab);
        if (decision != SubTabCaptureAction.Sample)
        {
            if (!passive && decision == SubTabCaptureAction.HideWrongTags)
            {
                recognitionHidden = true; surface.Hide(); Invalidate();
                // Let WPF/composition remove the old tags before sampling on the next tick.
            }
            return (null, sub is not null && decision == SubTabCaptureAction.Block
                ? "Page not recognised — sub-tab area blocked by tags" : null);
        }
        var sample = services.GetRequiredService<IScreenSampler>().TrySample(rect);
        return sample is null || sample.Width < PageSignature.Columns || sample.Height < PageSignature.Rows
            ? (null, null) : (PageSignature.Create(sample.Width, sample.Height, sample.Bgra), null);
    }
    private void TeachPage(bool explicitTeach = true, string successMessage = "Page remembered locally")
    {
        if (layout is null || references is null) { SetStatus("Load a page before teaching"); return; }
        var header = SampleRegion();
        if (header.Signature is null) { CaptureBlocked(header.Notice); return; }
        double[]? subSignature = null;
        if (region!.SubTabs.TryGetValue(layout.TabId, out var sub))
        {
            var captured = SampleRegion(sub, layout.TabId);
            subSignature = captured.Signature;
            if (subSignature is null) { CaptureBlocked(captured.Notice); return; }
        }
        try
        {
            references.Save(ClientResolution, layout.TabId + "/" + layout.Page, header.Signature, subSignature);
            // Teaching is optional and does not change the persisted automatic-display preference.
            ResetRecognition(); UpdatePolling(); SetStatus(successMessage);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { SetStatus("Could not save page reference"); }
    }
}
