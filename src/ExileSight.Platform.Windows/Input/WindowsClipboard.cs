using ExileSight.Platform.Abstractions.Input;
using ExileSight.Core.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ExileSight.Platform.Windows.Input;

/// <summary>Text-or-empty snapshot. Uses a private message-only STATIC window as clipboard owner.</summary>
public sealed class WindowsClipboard : IClipboard
{
    public uint SequenceNumber => PInvoke.GetClipboardSequenceNumber();
    public string? Save() => GetText();
    public void Restore(string? saved) => SetText(saved);
    private static void Open(HWND owner)
    {
        for (var i = 0; i < 20; i++)
        {
            if (PInvoke.OpenClipboard(owner)) return;
            Thread.Sleep(5);
        }
        throw new InvalidOperationException("Clipboard busy");
    }
    public unsafe string? GetText()
    {
        Open(default);
        try
        {
            var handle = PInvoke.GetClipboardData(13);
            if (handle.IsNull) return null;
            var size = PInvoke.GlobalSize(new HGLOBAL(handle.Value));
            if (size < 2 || size > 16 * 1024 * 1024 || size % 2 != 0)
                throw new InvalidOperationException("Invalid clipboard text size");
            var memory = PInvoke.GlobalLock(new HGLOBAL(handle.Value));
            if (memory == null) throw new InvalidOperationException("Clipboard lock failed");
            try
            {
                var text = new ReadOnlySpan<char>(memory, checked((int)(size / 2)));
                return ClipboardText.Decode(text);
            }
            finally { PInvoke.GlobalUnlock(new HGLOBAL(handle.Value)); }
        }
        finally { PInvoke.CloseClipboard(); }
    }
    public unsafe void SetText(string? text)
    {
        HWND owner;
        fixed (char* cls = "STATIC")
            owner = PInvoke.CreateWindowEx(0, cls, null, 0, 0, 0, 0, 0, new HWND(-3), default, default, null);
        if (owner.IsNull) throw new InvalidOperationException("Clipboard owner creation failed");
        try
        {
            Open(owner);
            try
            {
                if (!PInvoke.EmptyClipboard()) throw new InvalidOperationException("Clipboard clear failed");
                if (text is null) return;
                var memory = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)((text.Length + 1) * 2));
                if (memory.IsNull) throw new OutOfMemoryException();
                try
                {
                    var address = PInvoke.GlobalLock(memory);
                    if (address == null) throw new InvalidOperationException("Clipboard allocation lock failed");
                    try
                    {
                        text.AsSpan().CopyTo(new Span<char>(address, text.Length));
                        ((char*)address)[text.Length] = '\0';
                    }
                    finally { PInvoke.GlobalUnlock(memory); }
                    if (PInvoke.SetClipboardData(13, new HANDLE(memory.Value)).IsNull)
                        throw new InvalidOperationException("Clipboard write failed");
                    memory = default; // ownership transferred to the system
                }
                finally { if (!memory.IsNull) PInvoke.GlobalFree(memory); }
            }
            finally { PInvoke.CloseClipboard(); }
        }
        finally { PInvoke.DestroyWindow(owner); }
    }
}
