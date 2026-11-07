using System.Diagnostics;
using System.Text;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Windows.Input;
using Xunit;

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
}
