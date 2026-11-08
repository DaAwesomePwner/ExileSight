using ExileSight.Core.Items;

namespace ExileSight.Features.Vaalasync;

public sealed partial class ShopModel
{
    private sealed record ReadHistory(ParsedItem Item, string League, Guid Listing);
    private long readRevision;

    private async Task<ReadHistory?> RecordReadAsync(ParsedItem source, Task<string> resolution, Task previous, long read, DateTimeOffset time)
    {
        try
        {
            var resolved = await resolution.ConfigureAwait(false);
            await previous.ConfigureAwait(false);
            var current = ShopNote.Parse(source.Note);
            var id = current is null ? Guid.Empty : history.Observe(source.Name,
                source.BaseType ?? "", source.ItemLevel, resolved, current, time);
            NotifyChanged();
            return new(source, resolved, id);
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception error)
        {
            lock (sync)
            {
                if (readRevision == read)
                {
                    armed = false;
                    note = null;
                    status = "History unavailable; repricing disabled until the listing can be saved.";
                }
            }
            ReportSafely(error);
            NotifyChanged();
            return null;
        }
    }

    private async Task RecordSendAsync(Task<ReadHistory?> selected, Task previous, ShopNote price,
        string kind, DateTimeOffset time)
    {
        try
        {
            await previous.ConfigureAwait(false);
            if (await selected.ConfigureAwait(false) is not { } read) return;
            if (read.Listing == Guid.Empty)
                history.Observe(read.Item.Name, read.Item.BaseType ?? "", read.Item.ItemLevel,
                    read.League, price, time, kind);
            else history.Change(read.Listing, price, time, kind);
        }
        catch (Exception error)
        {
            ReportSafely(error);
            lock (sync) status += " — history could not be saved.";
        }
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        try { Changed?.Invoke(); }
        catch (Exception error) { ReportSafely(error); }
    }

    private void ReportSafely(Exception error)
    {
        try { ReportFailure(error.GetType().Name); }
        catch (Exception) { /* Diagnostics must not change input or history outcomes. */ }
    }
}
