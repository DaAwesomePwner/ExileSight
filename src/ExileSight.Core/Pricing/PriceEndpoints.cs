namespace ExileSight.Core.Pricing;

/// <summary>URL adapters are centralized; unique equipment uses the stash item overview.</summary>
public static class PriceEndpoints
{
    public static Uri Leagues { get; } = new("https://poe.ninja/poe1/api/data/index-state");
    public static Uri Overview(string league, string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(league);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        var exchange = type is "Currency" or "Fragment" or "Scarab" or "DivinationCard" or "Fossil" or
            "Resonator" or "Essence" or "Oil" or "DeliriumOrb" or "AllflameEmber" or "Astrolabe" or "EnshroudingCrystal";
        var path = exchange ? "exchange/current/overview" : "stash/current/item/overview";
        return new($"https://poe.ninja/poe1/api/economy/{path}?league={Uri.EscapeDataString(league).Replace("%20", "+")}&type={Uri.EscapeDataString(type)}");
    }
}
