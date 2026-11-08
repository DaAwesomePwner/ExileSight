namespace ExileSight.Core.Status;

public sealed record StatusTile(string Label, bool Ok, string Summary, string Detail);
public static class HomeStatus
{
    public static StatusTile Game(bool found, bool minimized, bool foreground) => new("Game", found && !minimized,
        !found ? "Game not found" : minimized ? "Game minimized" : "Path of Exile found",
        foreground ? "In front" : "Overlays wait until the game is in front.");
    public static StatusTile Log(string status) => new("Client.txt", status == "Following new entries",
        status == "Following new entries" ? "Reading the log" : "Log needs attention", status);
    public static StatusTile Prices(bool available, bool stale, string detail) => new("Prices", available && !stale,
        !available ? "Prices unavailable" : stale ? "Saved prices are stale" : "poe.ninja prices ready", detail);
    public static StatusTile Packs(string status) => new("Game data", status == "Packs ready",
        status == "Packs ready" ? "Item and map packs" : "Game data needs attention", status);
    public static string Title(IEnumerable<StatusTile> tiles) => tiles.FirstOrDefault(t => !t.Ok)?.Summary ?? "Ready to play";
}
