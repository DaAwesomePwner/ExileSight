// Frozen from 4ca85cc DefenceCalculator.cs; namespace/reference class name changed only.
using ExileSight.Core.DataPacks;
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo.Tests.Reference;

// Algorithm structure: Exile-UI modules/item-checker.ahk:412–465 (MIT).
// Formula per PoB/game: quality adds to local increased defence. This deliberately
// diverges from Exile-UI's quality treatment; integer bases must reproduce the rounded display.
internal static class DefenceCalculator
{
    public static IReadOnlyList<DefenceInfo> Calculate(ParsedItem item, IItemInfoData? data)
    {
        var basis = data?.FindBase(item);
        var stats = item.Unidentified || data is null ? null : DefenceStats.Read(item, data);
        return DefenceStats.Kinds.Where(k => item.Properties.ContainsKey(k.Property)).Select(k =>
        {
            decimal? percentile = null;
            if (stats is not null && basis?.Defences.TryGetValue(k.Key, out var range) == true &&
                item.Properties[k.Property].Values.FirstOrDefault() is { } displayed)
            {
                try
                {
                    var flat = stats.Where(v => v.StatId == k.Flat ||
                        k.Key == "ward" && v.StatId == "local_base_ward" ||
                        k.Key is "evasion" or "energy_shield" && v.StatId == "local_evasion_rating_and_energy_shield").Sum(v => v.Min);
                    var increased = stats.Where(v => v.StatId == k.Increased ||
                        DefenceStats.Hybrids.TryGetValue(v.StatId, out var keys) && keys.Contains(k.Key)).Sum(v => v.Min);
                    var multiplier = 1 + (increased + (item.Quality ?? 0)) / 100;
                    var raw = InferBase(displayed.Value, flat, multiplier, range);
                    if (raw is not null)
                        percentile = range.Max == range.Min ? 100 : (raw - range.Min) * 100 / (range.Max - range.Min);
                }
                catch (OverflowException) { /* Untrusted numeric copy: retain unknown. */ }
            }
            return new DefenceInfo(k.Property, percentile);
        }).ToArray();
    }

    private static decimal? InferBase(decimal displayed, decimal flat, decimal multiplier, NumericRange range)
    {
        if (multiplier <= 0 || range.Min > range.Max) return null;
        var rounded = Round(displayed / multiplier) - flat;
        // Integer display loses precision. Validate every candidate with the forward formula, including endpoints.
        decimal[] candidates = [rounded, rounded - 1, rounded + 1, range.Min, range.Max];
        bool Reproduces(decimal candidate) => candidate >= range.Min && candidate <= range.Max &&
            candidate == decimal.Truncate(candidate) && Round((candidate + flat) * multiplier) == displayed;
        foreach (var candidate in candidates.Distinct())
        {
            if (!Reproduces(candidate)) continue;
            // Rounding is monotonic: if multiple integer bases reproduce the display, an immediate
            // neighbour of any matching base also does. Check within the range, including endpoints.
            if (multiplier <= 1 && (Reproduces(candidate - 1) || Reproduces(candidate + 1))) return null;
            return candidate;
        }
        return null;
    }
    private static decimal Round(decimal value) => decimal.Round(value, 0, MidpointRounding.AwayFromZero);
}
