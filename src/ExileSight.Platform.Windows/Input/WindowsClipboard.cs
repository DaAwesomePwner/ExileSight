using System.Text;
using ExileSight.Core.Input;
using System.Runtime.InteropServices;
using ExileSight.Platform.Abstractions.Input;
using Windows.Win32;

namespace ExileSight.Platform.Windows.Input;

/// <summary>Preserves independently duplicated HGLOBAL formats; reports unsupported formats.</summary>
public sealed class WindowsClipboard : IClipboard
{
    private ClipboardOwner? offer;
    private bool lastConsumed;
    public int RecoveredFormats { get; private set; }
    public uint SequenceNumber => PInvoke.GetClipboardSequenceNumber();
    public bool PasteConsumed => offer?.Consumed ?? lastConsumed;
    public void Pump()
    {
        ClipboardOwner.Pump();
        if (offer?.RenderError is { } error) throw new InvalidOperationException("Clipboard rendering failed", error);
    }
    private static readonly BoundedClipboardRead<ClipboardSnapshot> snapshots = new();
    public ClipboardSnapshot Save() => InputTiming.Measure(InputStage.ClipboardSnapshot,
        () => snapshots.Read(SaveCore, pump: ClipboardOwner.Pump));
    private static ClipboardSnapshot SaveCore(CancellationToken cancellation)
    {
        ClipboardMemory.Open(default);
        try
        {
            var formats = new Dictionary<uint, byte[]>();
            var skipped = new List<uint>();
            var remaining = ClipboardMemory.MaximumBytes;
            uint format = 0;
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                Marshal.SetLastPInvokeError(0);
                format = PInvoke.EnumClipboardFormats(format);
                if (format == 0)
                {
                    if (Marshal.GetLastWin32Error() != 0) throw new InvalidOperationException("Clipboard enumeration failed");
                    break;
                }
                var bytes = ClipboardMemory.Read(format, remaining);
                cancellation.ThrowIfCancellationRequested();
                if (bytes is null) skipped.Add(format);
                else { formats.Add(format, bytes); remaining -= bytes.Length; }
            }
            // Refuse before mutation when nothing the user owns can be restored.
            if (formats.Count == 0 && skipped.Count > 0)
                throw new InvalidOperationException("No clipboard formats can be preserved");
            return new(formats, skipped);
        }
        finally { PInvoke.CloseClipboard(); }
    }
    private static readonly BoundedClipboardRead<string?> textReads = new();
    public string? GetText() => GetText(1500);
    public string? GetText(int timeoutMilliseconds) => InputTiming.Measure(InputStage.ClipboardRead,
        () => textReads.Read(_ => ReadText(), timeoutMilliseconds, ClipboardOwner.Pump));
    private static string? ReadText()
    {
        ClipboardMemory.Open(default);
        try
        {
            var bytes = ClipboardMemory.Read(13);
            if (bytes is null) return null;
            if (bytes.Length < 2 || bytes.Length % 2 != 0)
                throw new InvalidOperationException("Invalid clipboard text size");
            return ClipboardText.Decode(Encoding.Unicode.GetString(bytes));
        }
        finally { PInvoke.CloseClipboard(); }
    }
    public void SetText(string? text) => Restore(text is null ? null :
        new(new Dictionary<uint, byte[]> { [13] = Encoding.Unicode.GetBytes(text + '\0') }, []));
    public void OfferText(string text) => InputTiming.Measure(InputStage.ClipboardOffer, () =>
    {
        OfferCore(text);
        return true;
    });
    private void OfferCore(string text)
    {
        RecoveredFormats = 0;
        lastConsumed = false;
        offer?.Dispose();
        offer = new ClipboardOwner { Payload = Encoding.Unicode.GetBytes(text + '\0') };
        ClipboardMemory.Open(offer.Handle);
        try
        {
            if (!PInvoke.EmptyClipboard()) throw new InvalidOperationException("Clipboard clear failed");
            foreach (var name in new[] { "ExcludeClipboardContentFromMonitorProcessing",
                "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard" })
            {
                var format = ClipboardMemory.RegisterFormat(name);
                if (format == 0) throw new InvalidOperationException("Clipboard privacy format registration failed");
                ClipboardMemory.Write(format, new byte[4]);
            }
            // A NULL handle is both the expected delayed-render success and the failure value.
            Marshal.SetLastPInvokeError(0);
            var result = PInvoke.SetClipboardData(13, default);
            if (result.IsNull && Marshal.GetLastWin32Error() != 0 || !PInvoke.IsClipboardFormatAvailable(13))
                throw new InvalidOperationException("Delayed clipboard offer failed");
        }
        finally { PInvoke.CloseClipboard(); }
    }
    public void AbandonOffer()
    {
        if (offer is not null) { lastConsumed = offer.Consumed; offer.Payload = null; }
        offer?.Dispose();
        offer = null;
    }
    public void Restore(ClipboardSnapshot? saved) => InputTiming.Measure(InputStage.ClipboardRestore, () =>
    {
        RestoreCore(saved);
        return true;
    });
    private void RestoreCore(ClipboardSnapshot? saved)
    {
        using var temporary = ClipboardWorker.Owner is null ? new ClipboardOwner() : null;
        var owner = ClipboardWorker.Owner ?? temporary!;
        ClipboardMemory.Open(owner.Handle);
        try
        {
            if (!PInvoke.EmptyClipboard()) throw new InvalidOperationException("Clipboard clear failed");
            RecoveredFormats = 0;
            Exception? failure = null;
            foreach (var (format, bytes) in saved?.Formats ?? new Dictionary<uint, byte[]>())
            {
                try { ClipboardMemory.Write(format, bytes); RecoveredFormats++; }
                catch (Exception error) { failure = error; }
            }
            if (failure is not null) throw failure;
            AbandonOffer();
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }
}
