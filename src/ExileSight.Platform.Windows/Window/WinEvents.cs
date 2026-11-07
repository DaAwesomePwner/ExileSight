namespace ExileSight.Platform.Windows.Window;

internal static class WinEvents
{
    internal const uint Foreground = 0x0003;
    internal const uint MinimizeStart = 0x0016;
    internal const uint MinimizeEnd = 0x0017;
    internal const uint Destroy = 0x8001;
    internal const uint NameChange = 0x800C;
    internal const uint LocationChange = 0x800B;
}
