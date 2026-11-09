namespace ExileSight.Features.Stashninja;

public enum RecognitionAction { None, Hide, Restore, Select }

public sealed class RecognitionState
{
    private string? candidate;
    private int matches, unknowns;
    public bool Hidden { get; set; }
    public void Reset() { candidate = null; matches = unknowns = 0; }
    public RecognitionAction Observe(string? match, string? currentPage, bool captureSucceeded = true)
    {
        if (!captureSucceeded) { Reset(); return RecognitionAction.None; }
        if (match is null)
        {
            candidate = null; matches = 0;
            unknowns = Math.Min(3, unknowns + 1);
            if (unknowns < 3 || Hidden) return RecognitionAction.None;
            Hidden = true; return RecognitionAction.Hide;
        }
        unknowns = 0;
        matches = candidate == match ? Math.Min(2, matches + 1) : 1; candidate = match;
        if (matches < 2) return RecognitionAction.None;
        var wasHidden = Hidden; Hidden = false;
        if (match != currentPage) return RecognitionAction.Select;
        return wasHidden ? RecognitionAction.Restore : RecognitionAction.None;
    }
    public static bool EnableAfterTeach(bool explicitTeach, bool userPreference, bool enabled) =>
        explicitTeach || !userPreference || enabled;
}
