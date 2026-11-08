using ExileSight.Core.Items;

namespace ExileSight.Features.Vaalasync;

public sealed partial class ShopModel
{
    private sealed record ReadHistory(ParsedItem Item, string League, Guid Listing);
    private long readRevision;

    public async Task<string> ResolveHistoryLeagueAsync()
    {
        try { return await ResolveLeagueAsync().WaitAsync(historyStopping.Token).ConfigureAwait(false); }
        catch (Exception error)
        {
            var resolved = league();
            if (string.IsNullOrWhiteSpace(resolved)) resolved = "Standard";
            ReportSafely("League unconfirmed; recording history with fallback (" + error.GetType().Name + ").");
            return resolved;
        }
    }

    private async Task<ReadHistory?> RecordReadAsync(ParsedItem source, Task previous, long read, DateTimeOffset time)
    {
        try
        {
            var resolved = await ResolveHistoryLeagueAsync().ConfigureAwait(false);
            await previous.ConfigureAwait(false);
            var current = ShopNote.Parse(source.Note);
            var id = current is null ? Guid.Empty : history.Observe(source.Name,
                source.BaseType ?? "", source.ItemLevel, resolved, current, time);
            NotifyChanged();
            return new(source, resolved, id);
        }
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
            if (await selected.ConfigureAwait(false) is not { } read)
            {
                ReportSafely("Sent price history dropped: the read observation could not be saved.");
                return;
            }
            if (read.Listing == Guid.Empty)
                history.Observe(read.Item.Name, read.Item.BaseType ?? "", read.Item.ItemLevel,
                    read.League, price, time, kind);
            else history.Change(read.Listing, price, time, kind);
        }
        catch (Exception error)
        {
            ReportSafely("Sent price history could not be saved; entry dropped (" + error.GetType().Name + ").");
            lock (sync)
                if (ReferenceEquals(currentRead, selected)) status += " — history could not be saved.";
        }
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        try { Changed?.Invoke(); }
        catch (Exception error) { ReportSafely(error); }
    }

    private void ReportSafely(Exception error) => ReportSafely(error.GetType().Name);

    private void ReportSafely(string diagnostic)
    {
        try { ReportFailure(diagnostic); }
        catch (Exception) { /* Diagnostics must not change input or history outcomes. */ }
    }
}
