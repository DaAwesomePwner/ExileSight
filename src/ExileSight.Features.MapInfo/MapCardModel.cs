using System.Globalization;

namespace ExileSight.Features.MapInfo;

/// <summary>Immutable presentation data; unknown IDs never acquire a user rating.</summary>
public sealed class MapCardModel(MapResult result, MapInfoSettings settings, string? notice = null)
{
    public MapResult Result { get; } = result;
    public string? Notice { get; } = notice;
    public IReadOnlyList<MapRow> Rows { get; } = result.Mods.OrderBy(SortGroup).ToArray();
    public IEnumerable<MapRow> VisibleRows => Rows.Where(r => r.IsUnknown || r.Rank != 0);
    public IEnumerable<MapRow> FoldedRows => Rows.Where(r => !r.IsUnknown && r.Rank == 0);
    public int Deadly => Rows.Count(r => !r.IsUnknown && r.Rank == 3);
    public int Risky => Rows.Count(r => !r.IsUnknown && r.Rank == 2);
    public bool ShowFirstTimeHint => !Rows.Any(r => !r.IsUnknown && r.Rank.HasValue);
    public bool QuantityGood => Result.Quantity >= settings.QuantityThreshold;
    public bool RarityGood => Result.Rarity >= settings.RarityThreshold;
    public bool PackSizeGood => Result.PackSize >= settings.PackSizeThreshold;
    public string Metadata => string.Join(" · ", new[] { Result.Tier is { } tier ? $"T{tier}" : null,
        Result.ItemRarity, Result.Corrupted ? "corrupted" : null }.Where(s => s is not null));
    public static string AreaLabel(MapRow row) => row.IsUnknown ? "Unknown" : row.Area ?? (row.Category switch
    {
        "monster" or "monsters" => "Monsters", "player" or "players" => "Players",
        "boss" => "Unique boss", "area" => "Area",
        _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(row.Category ?? "")
    });
    public static int SortGroup(MapRow row) => row.IsUnknown ? 1 : row.Rank switch
    { 3 => 0, 2 => 2, 1 => 3, null => 4, _ => 5 };
}
