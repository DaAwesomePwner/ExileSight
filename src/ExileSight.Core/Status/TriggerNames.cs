using System.Collections.Frozen;
namespace ExileSight.Core.Status;

public static class TriggerNames
{
    public static IReadOnlyDictionary<string, string> All { get; } = Build();
    public static string Display(string id) => All.TryGetValue(id, out var name) ? name : id;
    private static FrozenDictionary<string, string> Build()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["copy-item"] = "Copy item", ["hideout"] = "Hideout macro" };
        void Both(string feature, string command, string name)
        { names[feature + "." + command] = name; names[feature + ":" + command] = name; }
        Both("omni", "press", "Omni key");
        Both("macros", "travel", "Hideout / travel wheel");
        Both("macros", "macro", "Chat macro / wheel");
        Both("timers", "hold", "Show timers while held");
        Both("timers", "toggle", "Toggle timers");
        Both("maptracker", "kills", "Request kill count");
        Both("maptracker", "viewer", "Map log");
        Both("events", "settings", "Event settings");
        Both("searchstr", "open", "Search strings");
        Both("guide", "next", "Next guide step");
        Both("guide", "back", "Previous guide step");
        Both("guide", "toggle", "Show or hide act guide");
        names["mapinfo:hold"] = "Show map card while held";
        names["searchstr:paste"] = "Search string button";
        Both("stashninja", "open", "Stash prices");
        names["stashninja:highlight"] = "Stash highlight button";
        foreach (var choice in new[] { "minus1", "plus1", "minus10", "plus10", "custom" })
            names["vaalasync:" + choice] = "Shop price button";
        for (var i = 0; i < 8; i++) names["macros:slot" + i] = "Chat wheel slot " + (i + 1);
        return names.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
