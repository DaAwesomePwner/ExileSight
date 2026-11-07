// Behaviour derived from Exile-UI (MIT), modules/map tracker.ahk and client log.ahk.
using ExileSight.Core.Logs;
namespace ExileSight.Features.MapTracker;

internal static class RunAreas
{
    internal static bool Side(string id) => id.Contains("SideArea", StringComparison.OrdinalIgnoreCase)
        || id.Contains("Abyss", StringComparison.OrdinalIgnoreCase)
        || id.StartsWith("Incursion", StringComparison.Ordinal) || id.StartsWith("Harvest", StringComparison.Ordinal);
    internal static bool Map(string id) => id.StartsWith("MapWorlds", StringComparison.Ordinal) && !Side(id);
    internal static bool Safe(string id) => id.Contains("Hideout", StringComparison.OrdinalIgnoreCase)
        || id.Contains("Town", StringComparison.OrdinalIgnoreCase)
        || id is "HeistHub" or "KaruiShores" or "Kingsmarch" or "KalguurSettlement";
    // These client diagnostics are not typed by the shared parser. Never inspect chat text.
    internal static bool SessionEnded(LogEvent value) => value is Unknown { Envelope: { } e }
        && e.Source.StartsWith("Client ", StringComparison.Ordinal)
        && (e.Message.StartsWith("Connecting to login server", StringComparison.Ordinal)
            || e.Message.StartsWith("Abnormal disconnect:", StringComparison.Ordinal)
            || e.Message == "Disconnected from server."
            || e.Message.StartsWith("***** LOG FILE OPENING", StringComparison.Ordinal));
}
