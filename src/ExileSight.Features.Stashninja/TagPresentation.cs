using System.Globalization;

namespace ExileSight.Features.Stashninja;

public static class TagPresentation
{
    public static string Price(decimal chaos, decimal? divine)
    {
        var value = divine is >= 1 ? divine.Value : chaos;
        var step = divine is >= 1 || chaos < 10 ? .1m : 1m;
        var format = "0.#";
        if (value is > 0 and < .1m)
        {
            // Decimal supports 28 fractional places. Stop at the first significant
            // digit, including the smallest positive decimal, rather than displaying zero.
            while (step > value) step /= 10;
            format = "0.############################";
        }
        // Truncate rather than round: the displayed price must not overstate value.
        return (value - value % step).ToString(format, CultureInfo.InvariantCulture) +
            (divine is >= 1 ? "d" : "");
    }

    // Prefer the full price; compact forms round DOWN to one decimal, then whole
    // units. Chaos >= 1000 uses k/m/b. Never round a positive price down to zero.
    public static string? FitPrice(string price, Func<string, bool> fits)
    {
        if (fits(price)) return price;
        var divine = price.EndsWith('d');
        if (!decimal.TryParse(divine ? price[..^1] : price, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var value) || value <= 0) return null;
        var suffix = divine ? "d" : "";
        if (!divine && value >= 1000)
        {
            var divisor = value >= 1_000_000_000 ? 1_000_000_000m : value >= 1_000_000 ? 1_000_000m : 1000m;
            suffix = divisor == 1000 ? "k" : divisor == 1_000_000 ? "m" : "b";
            value /= divisor;
        }
        foreach (var factor in new[] { 10m, 1m })
        {
            // Subtract the fractional remainder instead of multiplying large decimals.
            var rounded = value - value % (1 / factor);
            if (rounded <= 0) continue;
            var candidate = rounded.ToString("0.#", CultureInfo.InvariantCulture) + suffix;
            if (fits(candidate)) return candidate;
        }
        return null;
    }

    public static string Foreground(string background)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
        var rgb = Convert.FromHexString(background[1..]);
        var luminance = .2126 * Linear(rgb[0]) + .7152 * Linear(rgb[1]) + .0722 * Linear(rgb[2]);
        return (luminance + .05) / .05 >= 1.05 / (luminance + .05) ? "#000000" : "#FFFFFF";
    }

    public static PriceTag[] Highest(IEnumerable<PriceTag> tags) => tags.OrderByDescending(t => t.Chaos)
        .ThenBy(t => t.Name, StringComparer.Ordinal).Take(3).ToArray();

    public static string Age(DateTimeOffset? fetchedAt, DateTimeOffset now)
    {
        if (fetchedAt is null) return "price age unknown";
        var minutes = Math.Max(0, (now - fetchedAt.Value).TotalMinutes);
        var age = minutes < 1 ? "just now" : minutes < 60 ? $"{(int)minutes} min ago" :
            minutes < 1440 ? $"{(int)(minutes / 60)} h ago" : $"{(int)(minutes / 1440)} d ago";
        return age + (minutes >= 60 ? " · stale" : "");
    }
}
