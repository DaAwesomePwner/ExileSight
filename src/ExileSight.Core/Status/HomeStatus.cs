namespace ExileSight.Core.Status;

public sealed record StatusTile(string Label, bool Ok, string Summary, string Detail, bool Blocking = false);
public static class HomeStatus
{
    public static StatusTile Game(bool found, bool minimized, bool foreground) => new("Game", found && !minimized,
        !found ? "Game not found" : minimized ? "Game minimized" : "Path of Exile found",
        !found ? "Start Path of Exile to attach overlays." : minimized ? "Restore the game to see overlays." :
        foreground ? "In front" : "Overlays wait until the game is in front.", Blocking: !found || minimized);
    public static StatusTile Log(string status) => new("Client.txt", status == "Following new entries",
        status == "Following new entries" ? "Reading the log" : "Log needs attention", status, Blocking: status != "Following new entries");
    public static StatusTile Prices(bool available, bool stale, string detail) => new("Prices", available && !stale,
        !available ? "Prices unavailable" : stale ? "Saved prices are stale" : "poe.ninja prices ready", detail);
    public static StatusTile Packs(string status) => new("Game data", status == "Packs ready",
        status == "Packs ready" ? "Item and map packs" : "Game data needs attention", status, Blocking: status != "Packs ready");
    // Only unavailable/minimized game, unreadable log and unready packs block play.
    // Missing or stale prices are advisory; neither changes the readiness title.
    public static string Title(IEnumerable<StatusTile> tiles) => tiles.FirstOrDefault(t => t.Blocking)?.Summary ?? "Ready to play";
}
