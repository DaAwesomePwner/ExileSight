namespace ExileSight.Features.MapTracker;

public sealed record MapRun(int Schema, Guid Id, string League, string Map, int Tier,
    DateTime Started, DateTime Ended, double Seconds, int Portals, int Deaths, long? Kills,
    string[] Mechanics, string[] Mods, string Notes, bool Incomplete)
{
    // Schema-1 rows written before this flag had complete numeric counts by convention.
    public bool DeathsKnown { get; init; } = true;
    [System.Text.Json.Serialization.JsonIgnore]
    public string DeathsText => DeathsKnown ? Deaths.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unknown";
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string MechanicsText => string.Join("; ", Mechanics);
    [System.Text.Json.Serialization.JsonIgnore]
    public string ModsText => string.Join("; ", Mods);
}
