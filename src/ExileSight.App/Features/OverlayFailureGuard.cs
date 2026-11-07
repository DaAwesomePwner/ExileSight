namespace ExileSight.App.Features;

// Shared by dispatcher open/DPI callbacks; portable so native failures can be injected in tests.
public static class OverlayFailureGuard
{
    public static bool CanHandleDispatcherException(bool startupComplete, bool stopping) => startupComplete && !stopping;

    public static void Run(Action action, Action close, Action<Exception> log)
    {
        try { action(); }
        catch (Exception error)
        {
            try { close(); }
            catch (Exception cleanupError) { log(cleanupError); }
            log(error);
        }
    }
}
