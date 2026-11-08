namespace ExileSight.Core.Pricing;

internal static class PriceTypes
{
    public static string? FromCategory(string? category) => category?.Replace(" ", "").ToLowerInvariant() switch
    {
        "fossil" or "fossils" => "Fossil",
        "essence" or "essences" => "Essence",
        "oil" or "oils" => "Oil",
        "deliriumorb" or "deliriumorbs" => "DeliriumOrb",
        "resonator" or "resonators" => "Resonator",
        "scarab" or "scarabs" => "Scarab",
        "fragment" or "fragments" or "mapfragments" => "Fragment",
        "divinationcard" or "divinationcards" => "DivinationCard",
        "allflameember" or "allflameembers" => "AllflameEmber",
        "astrolabe" or "astrolabes" => "Astrolabe",
        "enshroudingcrystal" or "enshroudingcrystals" => "EnshroudingCrystal",
        "currency" => "Currency",
        _ => null
    };

    // Category grouping follows Exile-UI modules/stash-ninja.ahk:414-415.
    // Copied-item name heuristics below are ExileSight's adapter for those overview types.
    public static string? FromName(string name)
    {
        if (name.Contains("Scarab", StringComparison.OrdinalIgnoreCase)) return "Scarab";
        if (name.Contains("Essence of ", StringComparison.OrdinalIgnoreCase) || name == "Remnant of Corruption") return "Essence";
        if (name.Contains("Allflame Ember", StringComparison.OrdinalIgnoreCase)) return "AllflameEmber";
        foreach (var (suffix, type) in new[]
        {
            (" Fossil", "Fossil"), (" Oil", "Oil"), (" Delirium Orb", "DeliriumOrb"),
            (" Resonator", "Resonator"), (" Astrolabe", "Astrolabe"), ("Enshrouding Crystal", "EnshroudingCrystal")
        })
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return type;
        return null;
    }
}
