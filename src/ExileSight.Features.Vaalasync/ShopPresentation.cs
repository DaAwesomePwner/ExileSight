namespace ExileSight.Features.Vaalasync;

public sealed partial class ShopModel
{
    public ShopCardModel CardSnapshot(DateTimeOffset now, string? selectedAmount)
    {
        lock (sync) return ShopCardMapping.Map(new(CurrentItemName, note, FirstListedAt, currency,
            armed && !sending, NoteMode, Status, Instructions, CurrencyReminder,
            ShopCardMapping.Choices.ToDictionary(c => c.Choice, c => Suggest(c.Choice)), Suggest("custom")), now, selectedAmount);
    }

    // Never block the overlay on league discovery or disk history. Only the selected
    // read's listing is eligible; a trimmed price log cannot establish its first time.
    public string? CurrentItemName
    {
        get { lock (sync) return currentRead is { IsCompletedSuccessfully: true } ? currentRead.Result?.Item.Name : null; }
    }

    public DateTimeOffset? FirstListedAt
    {
        get
        {
            lock (sync)
            {
                if (currentRead is not { IsCompletedSuccessfully: true } || currentRead.Result is not { } read) return null;
                return history.FirstListedAt(read.Listing);
            }
        }
    }
}
