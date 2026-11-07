using System.Diagnostics;
using System.Text;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Windows.Input;
using Xunit;
using Windows.Win32;

namespace ExileSight.Platform.Windows.Tests.Input;

[Collection("Desktop input")]
public sealed class ClipboardRoundTripTests
{
    [WindowsOnlyFact]
    public void BinaryAndUnicodeFormatsSurviveAnActionPayload()
    {
        var clipboard = new WindowsClipboard();
        var original = clipboard.Save();
        try
        {
            var format = ClipboardMemory.RegisterFormat("ExileSight.RoundTripTest");
            Assert.NotEqual(0u, format);
            clipboard.Restore(new(new Dictionary<uint, byte[]>
            {
                [13] = Encoding.Unicode.GetBytes("user text\0"),
                [format] = [0, 255, 42, 0, 1]
            }, []));
            var saved = clipboard.Save();
            clipboard.OfferText("temporary command");
            clipboard.Restore(saved);
            var restored = clipboard.Save();
            Assert.Equal("user text", clipboard.GetText());
            Assert.Equal(saved.Formats[format], restored.Formats[format]);
        }
        finally { clipboard.Restore(original); }
    }

    [WindowsOnlyFact]
    public void DelayedRenderingServesReaderWithoutClaimingItWasTheGame()
    {
        var clipboard = new WindowsClipboard();
        var original = clipboard.Save();
        Task<string?>? read = null;
        try
        {
            clipboard.OfferText("delayed text");
            read = Task.Run(() => new WindowsClipboard().GetText());
            var timeout = Stopwatch.StartNew();
            while (!read.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(3))
            {
                clipboard.Pump();
                Thread.Sleep(1);
            }
            Assert.True(read.IsCompleted);
            Assert.Equal("delayed text", read.GetAwaiter().GetResult());
            Assert.False(clipboard.PasteConsumed);
        }
        finally
        {
            clipboard.Restore(original);
            if (read is not null) read.GetAwaiter().GetResult();
        }
    }
    [WindowsOnlyFact]
    public void OfferedTextOptsOutOfClipboardHistoryAndCloudProcessing()
    {
        var clipboard = new WindowsClipboard();
        var original = clipboard.Save();
        try
        {
            clipboard.OfferText("/hideout");
            ClipboardMemory.Open(default);
            try
            {
                foreach (var name in new[] { "ExcludeClipboardContentFromMonitorProcessing",
                    "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard" })
                {
                    var bytes = ClipboardMemory.Read(ClipboardMemory.RegisterFormat(name));
                    Assert.NotNull(bytes);
                    Assert.Equal(0u, BitConverter.ToUInt32(bytes));
                }
            }
            finally { PInvoke.CloseClipboard(); }
        }
        finally { clipboard.Restore(original); }
    }
    [WindowsOnlyFact]
    public void OversizedFormatsAreReportedAndDoNotPreventPreservingText()
    {
        var clipboard = new WindowsClipboard();
        var original = clipboard.Save();
        try
        {
            var format = ClipboardMemory.RegisterFormat("ExileSight.OversizeTest");
            clipboard.Restore(new(new Dictionary<uint, byte[]>
            {
                [13] = Encoding.Unicode.GetBytes("keep me\0"),
                [format] = new byte[ClipboardMemory.MaximumBytes + 1]
            }, []));
            var saved = clipboard.Save();
            Assert.Contains(format, saved.Skipped);
            Assert.DoesNotContain(format, saved.Formats.Keys);
            Assert.True(saved.Formats.Values.Sum(bytes => bytes.Length) <= ClipboardMemory.MaximumBytes);
            clipboard.SetText("temporary");
            clipboard.Restore(saved);
            Assert.Equal("keep me", clipboard.GetText());
        }
        finally { clipboard.Restore(original); }
    }

    [WindowsOnlyFact]
    public void RestorePumpsLateReaderWhileOpeningClipboard()
    {
        var clipboard = new WindowsClipboard();
        var original = clipboard.Save();
        using var held = new ManualResetEventSlim();
        Task<byte[]?>? reader = null;
        try
        {
            clipboard.OfferText("late reader");
            reader = Task.Run(() =>
            {
                ClipboardMemory.Open(default);
                try
                {
                    held.Set();
                    Thread.Sleep(50); // Model a foreign consumer that hitches while holding the clipboard.
                    return ClipboardMemory.Read(13);
                }
                finally { PInvoke.CloseClipboard(); }
            });
            Assert.True(held.Wait(5000));
            clipboard.Restore(original); // No explicit test-side pump: Open's retry path must service WM_RENDERFORMAT.
            var bytes = reader.GetAwaiter().GetResult();
            Assert.NotNull(bytes);
            Assert.Equal("late reader", ExileSight.Core.Input.ClipboardText.Decode(Encoding.Unicode.GetString(bytes)));
            var restored = clipboard.Save();
            foreach (var (format, saved) in original.Formats) Assert.Equal(saved, restored.Formats[format]);
        }
        finally
        {
            if (reader is not null)
            {
                while (!reader.IsCompleted) ClipboardOwner.Wait(5);
                reader.GetAwaiter().GetResult();
            }
            clipboard.Restore(original);
        }
    }

}
