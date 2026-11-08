namespace ExileSight.Platform.Windows.Input;

/// <summary>Worker-owned native infrastructure, prepared before hooks accept production input.</summary>
internal sealed class ClipboardWorker : IDisposable
{
    [ThreadStatic] private static ClipboardOwner? owner;
    internal static ClipboardOwner? Owner => owner;
    private ClipboardWorker() { }
    internal static ClipboardWorker Initialize()
    {
        owner = new ClipboardOwner();
        ClipboardOwner.Pump();
        return new();
    }
    public void Dispose()
    {
        owner?.Dispose();
        owner = null;
    }
}
