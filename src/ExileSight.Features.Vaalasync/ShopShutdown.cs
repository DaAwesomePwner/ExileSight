namespace ExileSight.Features.Vaalasync;

public sealed partial class ShopModel
{
    private readonly CancellationTokenSource historyStopping = new();
    private bool stoppingHistory;
    private TaskCompletionSource? sendFinished;

    /// <summary>Called by ApplicationStopping before host disposal; never waits for network discovery.</summary>
    public async Task StopAndFlushHistoryAsync(TimeSpan timeout)
    {
        Task activeSend;
        lock (sync)
        {
            stoppingHistory = true;
            armed = false;
            activeSend = sending ? sendFinished!.Task : Task.CompletedTask;
        }
        historyStopping.Cancel();
        try { await DrainHistoryAsync(activeSend).WaitAsync(timeout).ConfigureAwait(false); }
        catch (Exception error)
        {
            ReportSafely("Pending shop history not saved before shutdown; entries may be dropped (" +
                error.GetType().Name + ").");
        }
    }

    private async Task DrainHistoryAsync(Task activeSend)
    {
        await activeSend.ConfigureAwait(false);
        Task pending;
        lock (sync) pending = historyTail;
        await pending.ConfigureAwait(false);
    }
}
