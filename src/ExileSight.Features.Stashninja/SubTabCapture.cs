namespace ExileSight.Features.Stashninja;

public enum SubTabCaptureAction { Sample, Block, HideWrongTags }

public static class SubTabCapture
{
    public static SubTabCaptureAction Decide(bool overlaps, string? drawnTab, string? matchedTab)
    {
        if (!overlaps) return SubTabCaptureAction.Sample;
        // A confirmed different tab makes the old tags wrong. Own-tab overlap is
        // persistent geometry, so hiding and restoring those tags would flicker.
        return drawnTab is not null && matchedTab is not null && drawnTab != matchedTab
            ? SubTabCaptureAction.HideWrongTags : SubTabCaptureAction.Block;
    }
}
