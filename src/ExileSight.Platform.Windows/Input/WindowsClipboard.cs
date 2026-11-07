using System.Text;
using System.Runtime.InteropServices;
using ExileSight.Platform.Abstractions.Input;
using Windows.Win32;

namespace ExileSight.Platform.Windows.Input;

/// <summary>Preserves independently duplicated HGLOBAL formats; reports unsupported formats.</summary>
public sealed class WindowsClipboard : IClipboard
{
    private ClipboardOwner? offer;
    public uint SequenceNumber => PInvoke.GetClipboardSequenceNumber();
    public bool PasteConsumed => offer?.Consumed == true;
    public void Pump() => ClipboardOwner.Pump();
    public ClipboardSnapshot Save()
    {
        ClipboardMemory.Open(default);
        try
        {
            var formats = new Dictionary<uint, byte[]>();
            var skipped = new List<uint>();
            uint format = 0;
            while (true)
            {
                Marshal.SetLastPInvokeError(0);
                format = PInvoke.EnumClipboardFormats(format);
                if (format == 0)
                {
                    if (Marshal.GetLastWin32Error() != 0) throw new InvalidOperationException("Clipboard enumeration failed");
                    break;
                }
                var bytes = ClipboardMemory.Read(format);
                if (bytes is null) skipped.Add(format);
                else formats.Add(format, bytes);
            }
            // Refuse before mutation when nothing the user owns can be restored.
            if (formats.Count == 0 && skipped.Count > 0)
                throw new InvalidOperationException("No clipboard formats can be preserved");
            return new(formats, skipped);
        }
        finally { PInvoke.CloseClipboard(); }
    }
    public string? GetText()
    {
        ClipboardMemory.Open(default);
        try
        {
            var bytes = ClipboardMemory.Read(13);
            if (bytes is null) return null;
            var text = Encoding.Unicode.GetString(bytes);
            var end = text.IndexOf('\0');
            return end < 0 ? text : text[..end];
        }
        finally { PInvoke.CloseClipboard(); }
    }
    public void SetText(string? text) => Restore(text is null ? null :
        new(new Dictionary<uint, byte[]> { [13] = Encoding.Unicode.GetBytes(text + '\0') }, []));
    public void OfferText(string text)
    {
        offer?.Dispose();
        offer = new ClipboardOwner { Payload = Encoding.Unicode.GetBytes(text + '\0') };
        ClipboardMemory.Open(offer.Handle);
        try
        {
            if (!PInvoke.EmptyClipboard()) throw new InvalidOperationException("Clipboard clear failed");
            PInvoke.SetClipboardData(13, default); // NULL is expected for delayed rendering.
        }
        finally { PInvoke.CloseClipboard(); }
    }
    public void Restore(ClipboardSnapshot? saved)
    {
        using var owner = new ClipboardOwner();
        ClipboardMemory.Open(owner.Handle);
        try
        {
            if (!PInvoke.EmptyClipboard()) throw new InvalidOperationException("Clipboard clear failed");
            Exception? failure = null;
            foreach (var (format, bytes) in saved?.Formats ?? new Dictionary<uint, byte[]>())
            {
                try { ClipboardMemory.Write(format, bytes); }
                catch (Exception error) { failure = error; }
            }
            if (failure is not null) throw failure;
        }
        finally
        {
            PInvoke.CloseClipboard();
            offer?.Dispose();
            offer = null;
        }
    }
}
