using ExileSight.Core.Pricing;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Vaalasync;

public static class Conversions
{
    public static string Describe(ShopNote note, PriceResult result)
    {
        var overview = result.Overview;
        var name = note.Currency switch
        {
            "chaos" => "Chaos Orb", "divine" => "Divine Orb", "exalted" => "Exalted Orb",
            "mirror" => "Mirror of Kalandra", "transmut" => "Orb of Transmutation",
            "aug" => "Orb of Augmentation", "regal" => "Regal Orb", "alch" => "Orb of Alchemy",
            "alt" => "Orb of Alteration", "fusing" => "Orb of Fusing", "vaal" => "Vaal Orb",
            "chisel" => "Cartographer's Chisel", "scour" => "Orb of Scouring", "regret" => "Orb of Regret",
            _ => note.Currency
        };
        decimal? rate = note.Currency == "chaos" ? 1 : overview?.Find(name)?.ChaosValue;
        var chaos = rate * note.Amount;
        var divine = chaos is { } c ? overview?.ToDivine(c) : null;
        return $"{(chaos is null ? "rate unavailable" : $"{GameAction.FormatAmount(decimal.Round(chaos.Value, 2))} chaos / {(divine is null ? "" : GameAction.FormatAmount(decimal.Round(divine.Value, 4)))} divine")} · " +
            $"{(result.IsStale ? "stale" : "cached")} {result.UpdatedAt:yyyy-MM-dd HH:mm}";
    }
}
