using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace ExileSight.Platform.Windows.Input;

internal static class ClipboardMemory
{
    internal static unsafe uint RegisterFormat(string name)
    {
        fixed (char* value = name) return PInvoke.RegisterClipboardFormat(value);
    }
    internal static void Open(HWND owner)
    {
        for (var i = 0; i < 20; i++)
        {
            if (PInvoke.OpenClipboard(owner)) return;
            Thread.Sleep(5);
        }
        throw new InvalidOperationException("Clipboard busy");
    }
    internal const int MaximumBytes = 16 * 1024 * 1024;
    internal static unsafe byte[]? Read(uint format, int remaining = MaximumBytes)
    {
        if (remaining <= 0) return null; // Do not force a delayed provider once the budget is exhausted.
        // These formats contain GDI handles or nested handles, not portable HGLOBAL bytes.
        if (format is 2 or 3 or 9 or 14 or 0x80 or 0x82 or 0x83 or 0x8e or >= 0x300 and <= 0x3ff) return null;
        var handle = PInvoke.GetClipboardData(format);
        if (handle.IsNull) return null;
        var global = new HGLOBAL(handle.Value);
        var size = PInvoke.GlobalSize(global);
        if (size == 0 || size > (nuint)Math.Min(remaining, MaximumBytes)) return null;
        var pointer = PInvoke.GlobalLock(global);
        if (pointer == null) return null;
        try { return new ReadOnlySpan<byte>(pointer, (int)size).ToArray(); }
        finally { PInvoke.GlobalUnlock(global); }
    }
    internal static unsafe void Write(uint format, byte[] bytes)
    {
        var memory = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)bytes.Length);
        if (memory.IsNull) throw new OutOfMemoryException();
        try
        {
            var pointer = PInvoke.GlobalLock(memory);
            if (pointer == null) throw new InvalidOperationException("Clipboard allocation lock failed");
            try { bytes.CopyTo(new Span<byte>(pointer, bytes.Length)); }
            finally { PInvoke.GlobalUnlock(memory); }
            if (PInvoke.SetClipboardData(format, new HANDLE(memory.Value)).IsNull)
                throw new InvalidOperationException("Clipboard write failed");
            memory = default;
        }
        finally { if (!memory.IsNull) PInvoke.GlobalFree(memory); }
    }
}
