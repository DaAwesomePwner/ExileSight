namespace ExileSight.Core.DataPacks;

// Semantics: PyPoE/poe/file/translations.py TranslationQuantifier definitions (linked in DataGen README).
// Use division, not a pre-rounded reciprocal. Raw RePoE stats are integers.
internal sealed record StatTransform(decimal Multiplier, decimal Divisor, int? Digits = null, bool Floor = false)
{
    public decimal Apply(decimal raw)
    {
        var value = raw * Multiplier / Divisor;
        return Floor ? decimal.Floor(value) : Digits is int digits ? decimal.Round(value, digits, MidpointRounding.ToEven) : value;
    }

    public bool Reverse(decimal displayed, out decimal min, out decimal max)
    {
        min = max = 0;
        try
        {
            var center = displayed * Divisor / Multiplier;
            if (Digits is null && !Floor)
            {
                if (center != decimal.Truncate(center) || Apply(center) != displayed) return false;
                min = max = center;
                return true;
            }
            var unit = Digits switch { 1 => .1m, 2 => .01m, _ => 1m };
            var radius = unit * Math.Abs(Divisor / Multiplier) + 1;
            var start = Math.Max(int.MinValue, decimal.Floor(center - radius));
            var end = Math.Min(int.MaxValue, decimal.Ceiling(center + radius));
            if (end - start > 10000) return false;
            var found = false;
            for (var raw = start; raw <= end; raw++)
            {
                if (Apply(raw) != displayed) continue;
                if (!found) min = raw;
                max = raw;
                found = true;
            }
            return found;
        }
        catch (OverflowException) { return false; }
    }
}

internal static class StatHandlers
{
    public static StatTransform? Resolve(string[] handlers)
    {
        var result = new StatTransform(1, 1);
        foreach (var handler in handlers)
        {
            if (handler is "canonical_line" or "canonical_stat") continue;
            // Multiple rounding stages need a different inverse; never silently flatten them.
            if (result.Digits is not null || result.Floor) return null;
            var next = Single(handler);
            if (next is null) return null;
            result = new(result.Multiplier * next.Multiplier, result.Divisor * next.Divisor, next.Digits, next.Floor);
        }
        return result;
    }

    private static StatTransform? Single(string name)
    {
        var digits = name.EndsWith("_0dp", StringComparison.Ordinal) ? 0 :
            name.Contains("_1dp", StringComparison.Ordinal) ? 1 :
            name.Contains("_2dp", StringComparison.Ordinal) ? 2 : (int?)null;
        var stem = name.Split("_0dp")[0].Split("_1dp")[0].Split("_2dp")[0];
        return stem switch
        {
            "negate" => new(-1, 1), "double" => new(2, 1), "negate_and_double" => new(-2, 1),
            "per_minute_to_per_second" => new(1, 60, digits ?? 1),
            "milliseconds_to_seconds" => new(1, 1000, digits),
            "divide_by_one_hundred" => new(1, 100, digits),
            "divide_by_one_thousand" => new(1, 1000, digits),
            "divide_by_ten" => new(1, 10, digits, digits == 0),
            "divide_by_two" => new(1, 2, digits, digits == 0),
            "divide_by_fifteen" => new(1, 15, digits, digits == 0),
            "deciseconds_to_seconds" or "locations_to_metres" => new(1, 10),
            "old_leech_percent" or "divide_by_five" => new(1, 5),
            "old_leech_permyriad" => new(1, 500),
            "divide_by_three" => new(1, 3), "divide_by_six" => new(1, 6),
            "divide_by_twelve" => new(1, 12), "divide_by_four" => new(1, 4),
            "divide_by_twenty" => new(1, 20),
            "times_twenty" => new(20, 1), "times_one_point_five" => new(3, 2),
            "30%_of_value" => new(3, 10), "60%_of_value" => new(3, 5),
            "divide_by_one_hundred_and_negate" => new(-1, 100),
            _ => null
        };
    }
}
