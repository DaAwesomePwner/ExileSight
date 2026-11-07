using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.App.Features.SearchStrings;

// Portable click lifecycle: preparation is guarded, and every exit clears the selection.
public static class SearchClick
{
    public static async Task RunAsync(Action prepare, Func<IntentToken?> redeem,
        Func<IntentToken, Task<ActionResult>> execute, Action cancel, Action close, Action<Exception> log)
    {
        try
        {
            IntentToken? token = null;
            OverlayFailureGuard.Run(() => { prepare(); token = redeem(); }, cancel, log);
            if (token is not null) await execute(token);
        }
        catch (Exception error) { log(error); }
        finally
        {
            OverlayFailureGuard.Run(() => { cancel(); close(); }, cancel, log);
        }
    }
}
