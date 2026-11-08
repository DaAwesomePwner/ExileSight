using System.Text.Json;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Vaalasync;

public sealed partial class ShopModel
{
    private static readonly string[] Adjustments = ["minus1", "plus1", "minus10", "plus10"];
    public bool SavedNoteMode => context?.ReadSetting("listingMode") is { ValueKind: JsonValueKind.String } mode && mode.GetString() == "note";
    public string ListingModeLabel => "Use legacy note mode" + (SavedNoteMode != NoteMode ? " (applies after restart)" : "");
    public string CurrencyReminder
    {
        get { lock (sync) return NoteMode || currentCurrency == currency ? "" : "Switch the game’s currency dropdown to " + currency + "."; }
    }
    public string DisplayStatus => CurrencyReminder is { Length: > 0 } reminder ? Status + "\n" + reminder : Status;
    public string BaselineDescription
    {
        get
        {
            lock (sync) return note is not null ? "Current price: " + note.DisplayAmount : estimate is not null
                ? "No current price — poe.ninja estimate: " + estimate.DisplayAmount : "No current price";
        }
    }
    internal static decimal WholeAmount(decimal amount) => Math.Max(1, decimal.Round(amount, 0, MidpointRounding.AwayFromZero));
    public ShopNote? Suggest(string choice)
    {
        lock (sync)
        {
            if (ShopNote.Parse($"~b/o 1 {currency}") is null) return null;
            if (choice == "custom")
            {
                var amount = ShopNote.ParseAmount(custom);
                if (amount is null || (!NoteMode && amount != decimal.Truncate(amount.Value))) return null;
                var customNote = new ShopNote(note?.Prefix ?? "~b/o", amount.Value, currency);
                return NoteMode ? customNote.Suggest("custom", custom) : customNote;
            }
            if (note is null) return null;
            var baseline = note with { Currency = currency };
            if (NoteMode) return baseline.Suggest(choice);
            var seen = new HashSet<decimal>();
            foreach (var adjustment in Adjustments)
            {
                var raw = adjustment switch
                {
                    "minus1" => note.Amount - 1, "plus1" => note.Amount + 1,
                    "minus10" => note.Amount * .9m, _ => note.Amount * 1.1m
                };
                var amount = WholeAmount(raw);
                var available = amount <= ShopNote.Maximum && seen.Add(amount);
                if (choice == adjustment) return available ? baseline with { Amount = amount } : null;
            }
            return null;
        }
    }
    public string? SaveCustom(string amountText, string currencyText)
    {
        var parsedCurrency = ShopNote.Parse("~b/o 1 " + currencyText.Trim());
        if (parsedCurrency is null) return "Invalid currency.";
        var amount = ShopNote.ParseAmount(amountText.Trim());
        if (amount is null) return "Invalid amount.";
        if (!NoteMode && amount != decimal.Truncate(amount.Value)) return "Async listing amounts must be a whole number.";
        lock (sync)
        {
            currency = parsedCurrency.Currency;
            custom = GameAction.FormatAmount(amount.Value);
            revision++;
        }
        Changed?.Invoke();
        return null;
    }
}
