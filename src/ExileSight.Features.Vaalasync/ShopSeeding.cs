using ExileSight.Platform.Abstractions.Input;
using System.Text.Json;
using ExileSight.Core.Items;

namespace ExileSight.Features.Vaalasync;

public sealed partial class ShopModel
{
    public void SaveListingMode(bool useNotes) => context!.WriteSetting("listingMode",
        JsonSerializer.SerializeToElement(useNotes ? "note" : "async"));

    private async Task PrepareAsync(ParsedItem source, long expectedRevision, long expectedRead)
    {
        try
        {
            var resolved = await ResolveLeagueAsync().ConfigureAwait(false);
            lock (sync)
            {
                if (readRevision != expectedRead || sending) return;
                var current = ShopNote.Parse(source.Note);
                itemLeague = resolved;
                listing = current is null ? Guid.Empty : history.Observe(source.Name,
                    source.BaseType ?? "", source.ItemLevel, resolved, current, DateTimeOffset.UtcNow);
                note = current;
                armed = true;
                status = Instructions;
            }
            Changed?.Invoke();
            if (ShopNote.Parse(source.Note) is null) await SeedAsync(source, resolved, expectedRevision).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            lock (sync)
            {
                if (readRevision != expectedRead) return;
                status = "History or league unavailable; repricing disabled until the item can be saved.";
            }
            try { ReportFailure(error.GetType().Name); } catch (Exception) { }
            Changed?.Invoke();
        }
    }

    private async Task SeedAsync(ParsedItem source, string resolvedLeague, long expectedRevision)
    {
        try
        {
            var seed = await SeedPrice(source, resolvedLeague).ConfigureAwait(false);
            if (seed is null || ShopNote.Parse(seed.ToString()) is null) return;
            lock (sync)
            {
                // A late price response must not overwrite edits, a send or a newer item read.
                if (revision != expectedRevision || !armed || sending) return;
                estimate = seed;
                currency = seed.Currency;
                custom = GameAction.FormatAmount(NoteMode ? seed.Amount : WholeAmount(seed.Amount));
            }
            Changed?.Invoke();
        }
        catch (Exception error)
        {
            try { ReportFailure(error.GetType().Name); }
            catch (Exception) { /* Price lookup is optional; never send input or fail the menu. */ }
        }
    }
}
