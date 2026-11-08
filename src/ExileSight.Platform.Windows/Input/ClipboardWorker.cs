namespace ExileSight.Platform.Windows.Input;

/// <summary>Warm native window creation before input starts without retaining a clipboard owner.</summary>
internal static class ClipboardWorker
{
    internal static void Initialize()
    {
        using var owner = new ClipboardOwner();
        ClipboardOwner.Pump();
    }
}
