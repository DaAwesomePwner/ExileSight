using ExileSight.Platform.Windows.Input;
using Windows.Win32;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Input;

[Collection("Desktop input")]
public sealed class ClipboardOwnerLifetimeTests
{
    [WindowsOnlyFact]
    public void OtherThreadCanEmptyAfterRestoreWhileActionWorkerDoesNotPump()
    {
        var clipboard = new WindowsClipboard();
        var original = clipboard.Save();
        using var restored = new ManualResetEventSlim();
        using var releaseWorker = new ManualResetEventSlim();
        Task? empty = null;
        var worker = Task.Factory.StartNew(() =>
        {
            ClipboardWorker.Initialize();
            new WindowsClipboard().SetText("restored user text");
            restored.Set();
            Assert.True(releaseWorker.Wait(TimeSpan.FromSeconds(30))); // deliberately no pump
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.True(restored.Wait(TimeSpan.FromSeconds(10)));
            empty = Task.Factory.StartNew(() =>
            {
                ClipboardMemory.Open(default);
                try { Assert.True(PInvoke.EmptyClipboard()); }
                finally { PInvoke.CloseClipboard(); }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.True(empty.Wait(TimeSpan.FromSeconds(5)), "EmptyClipboard blocked on the idle action worker");
            Assert.False(worker.IsCompleted);
        }
        finally
        {
            releaseWorker.Set();
            // Terminating the dedicated thread also releases its windows on a failing regression.
            Assert.True(worker.Wait(TimeSpan.FromSeconds(10)));
            if (empty is not null) Assert.True(empty.Wait(TimeSpan.FromSeconds(10)));
            clipboard.Restore(original);
        }
    }
}
