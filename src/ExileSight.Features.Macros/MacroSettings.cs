using System.Text.Json;

namespace ExileSight.Features.Macros;

public sealed record MacroSettings(string[] Commands, string[] Destinations, string Command0 = "")
{
    // Travel names verified against Exile-UI modules/macros.ahk (MIT).
    public static MacroSettings Default => new(["/dnd", "", "", "", "", "", "", ""],
        ["/hideout", "/menagerie", "", "", "", "", "", ""], "/dnd");
    public static bool ValidText(string? text) => text is not null && text.Length <= 400 &&
        !text.Any(c => char.IsControl(c) || c is '\u2028' or '\u2029');
    public bool IsValid => ValidText(Command0) && ValidList(Commands, false) && ValidList(Destinations, true);
    private static bool ValidList(string[]? values, bool slashOnly) => values is { Length: 8 } &&
        values.All(s => ValidText(s) && (!slashOnly || s.Length == 0 || s.StartsWith('/')));
    public static MacroSettings Read(JsonElement? json)
    {
        if (json is null) return Default;
        try
        {
            var result = json.Value.Deserialize<MacroSettings>();
            if (result?.IsValid == true)
            {
                // Legacy Commands[0] served both purposes. Preserve explicit new Command0,
                // including blank, and never overwrite any of the eight wheel slots.
                return json.Value.TryGetProperty(nameof(Command0), out _) ? result :
                    result with { Command0 = result.Commands[0] };
            }
        }
        catch (JsonException) { }
        // Malformed persisted configuration must never fall back to an unexpected command.
        return new(new string[8].Select(_ => "").ToArray(), new string[8].Select(_ => "").ToArray());
    }
}
