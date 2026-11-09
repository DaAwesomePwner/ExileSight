using System.Globalization;

namespace ExileSight.Features.Stashninja;

public static class TagPresentation
{
    public static string Price(decimal chaos, decimal? divine) => divine is >= 1
        ? divine.Value.ToString("0.##", CultureInfo.InvariantCulture) + "d"
        : chaos.ToString("0.##", CultureInfo.InvariantCulture);

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
