using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Vaalasync;

public sealed record ShopCardInput(string? Name, ShopNote? Note, DateTimeOffset? FirstListedAt,
    string Currency, bool Armed, bool NoteMode, string Status, string Instructions, string CurrencyReminder,
    IReadOnlyDictionary<string, ShopNote?> Suggestions, ShopNote? Custom);

public static class ShopCardMapping
{
    public static IReadOnlyList<(string Choice, string Label)> Choices { get; } = Array.AsReadOnly(new[]
    {
        ("minus10", "−10%"), ("minus1", "−1"), ("plus1", "+1"), ("plus10", "+10%")
    });

    public static ShopCardModel Map(ShopCardInput input, DateTimeOffset now, string? selectedAmount)
    {
        var amounts = Choices.Select(choice =>
        {
            var suggestion = input.Suggestions[choice.Choice];
            var display = suggestion;
            if (display is null && !input.NoteMode && input.Note is { } note)
            {
                var raw = choice.Choice switch { "minus10" => note.Amount * .9m, "minus1" => note.Amount - 1,
                    "plus1" => note.Amount + 1, _ => note.Amount * 1.1m };
                display = note with { Amount = ShopModel.WholeAmount(raw) };
            }
            return new ShopAmount(choice.Choice, choice.Label, display is null ? "—" : input.NoteMode
                ? display.ToString() : GameAction.FormatAmount(display.Amount), suggestion is not null);
        }).ToArray();
        var pasted = !input.Armed && input.Status.Contains(ShopModel.RearmMessage, StringComparison.Ordinal)
            ? selectedAmount : null;
        // Strip only the redundant instruction, preserving recovery notices, paste diagnostics and currency reminders.
        var status = input.Status.Replace(ShopModel.RearmMessage, "", StringComparison.Ordinal)
            .Replace(input.Instructions, "", StringComparison.Ordinal).Trim();
        var notice = string.Join("\n", new[] { status, input.CurrencyReminder }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new(input.Name ?? "Shop pricer", input.Note?.DisplayAmount,
            input.FirstListedAt is { } first ? now - first : null, input.Currency, input.Armed, input.NoteMode, amounts,
            input.Custom is { } custom ? input.NoteMode ? custom.ToString() : custom.DisplayAmount : null, pasted, notice);
    }
}
