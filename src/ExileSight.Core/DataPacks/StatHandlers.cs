namespace ExileSight.Core.DataPacks;

// RePoE handler names. Non-numeric lookup and lossy rounding handlers deliberately fail closed.
internal static class StatHandlers
{
    public static bool Scale(string[] handlers, out decimal scale)
    {
        scale = 1;
        foreach (var handler in handlers)
        {
            decimal? factor = handler switch
            {
                "negate" => -1, "double" => 2, "negate_and_double" => -2,
                "per_minute_to_per_second" => 1m / 60,
                "milliseconds_to_seconds" => .001m,
                "divide_by_one_hundred" => .01m,
                "divide_by_one_thousand" => .001m,
                "deciseconds_to_seconds" => .1m,
                "divide_by_ten_1dp_if_required" => .1m,
                "locations_to_metres" => .1m,
                "times_twenty" => 20, "times_one_point_five" => 1.5m,
                "divide_by_five" => .2m, "divide_by_four" => .25m,
                "divide_by_twenty" => .05m,
                "divide_by_one_hundred_and_negate" => -.01m,
                _ => null
            };
            if (factor is null) return false;
            scale *= factor.Value;
        }
        return true;
    }
    public static decimal Apply(decimal value, decimal scale) => decimal.Round(value * scale, 12);
    public static decimal Reverse(decimal value, decimal scale) => decimal.Round(value / scale, 12);
}
