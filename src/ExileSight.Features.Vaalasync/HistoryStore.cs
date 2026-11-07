// Provenance: Exile-UI modules/exchange.ahk:136–148,760–860,924–990.
// Local bounded history; whispers are uncertain evidence and never mark a sale.
using System.Text.Json;
using ExileSight.Core.Logs;

namespace ExileSight.Features.Vaalasync;

public sealed record PriceEntry(DateTimeOffset Time, ShopNote Note, string Kind);
public sealed record WhisperEvidence(string Item, string Price, DateTimeOffset Time);
public sealed record Listing(Guid Id, string Item, string Base, int? ItemLevel, string League,
    PriceEntry[] Prices, DateTimeOffset? Sold = null, WhisperEvidence? Evidence = null, bool Purchase = false);

public sealed class HistoryStore
{
    public const int MaxListings = 500, MaxPrices = 50, MaxBytes = 8 * 1024 * 1024;
    private readonly string path;
    private readonly object sync = new();
    private Listing[] rows = [];
    private bool unavailable;
    public string? RecoveryNotice { get; private set; }
    public HistoryStore(string directory)
    {
        path = Path.Combine(directory, "history.json");
        try
        {
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length > MaxBytes) throw new IOException("Shop history exceeds size limit.");
            var loaded = JsonSerializer.Deserialize<Listing[]>(File.ReadAllText(path));
            Validate(loaded);
            rows = Bound(loaded!);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            var kept = path + "." + DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "." + Guid.NewGuid().ToString("N") + ".corrupt";
            try { File.Move(path, kept); }
            catch (Exception moveError) when (moveError is IOException or UnauthorizedAccessException)
            {
                kept = path;
                unavailable = true; // Never overwrite an original that could not be quarantined.
            }
            RecoveryNotice = "History unavailable — previous file kept as " + kept;
        }
    }
    public Listing[] Snapshot() { lock (sync) return rows.Select(r => r with { Prices = r.Prices.ToArray() }).ToArray(); }
    public Guid Observe(string item, string baseType, int? level, string league, ShopNote note, DateTimeOffset now)
    {
        lock (sync)
        {
            var existing = rows.FirstOrDefault(r => !r.Purchase && r.Sold is null && r.Item == item && r.Base == baseType && r.ItemLevel == level && r.League == league);
            if (existing is not null)
            {
                if (existing.Prices.LastOrDefault()?.Note != note) Change(existing.Id, note, now, "Observed");
                return existing.Id;
            }
            var row = new Listing(Guid.NewGuid(), Clean(item), Clean(baseType), level, Clean(league), [new(now, note, "Listed")]);
            Save([.. rows, row]);
            return row.Id;
        }
    }
    public void Change(Guid id, ShopNote note, DateTimeOffset now, string kind = "Sent") => Update(id,
        row => row with { Prices = [.. row.Prices.TakeLast(MaxPrices - 1), new(now, note, kind)] });
    public void MarkSold(Guid id, DateTimeOffset now) => Update(id, row => row with { Sold = now });
    public void RecordPurchase(string item, string league, ShopNote note, DateTimeOffset now)
    {
        lock (sync) Save([.. rows, new(Guid.NewGuid(), Clean(item), "", null, Clean(league), [new(now, note, "Purchase")], Purchase: true)]);
    }
    public void Evidence(TradeWhisper whisper, DateTimeOffset now)
    {
        if (whisper.Direction != "@From" || ShopNote.Parse("~b/o " + whisper.Price) is not { } note) return;
        lock (sync)
        {
            var matching = rows.Where(r => !r.Purchase && r.Sold is null && r.League == whisper.League &&
                (r.Item == whisper.Item || r.Base == whisper.Item || r.Item + " " + r.Base == whisper.Item) &&
                r.Prices.LastOrDefault()?.Note is { } price &&
                price.Amount == note.Amount && price.Currency == note.Currency).Select(r => r.Id).ToHashSet();
            if (matching.Count == 0) return;
            Save(rows.Select(r => matching.Contains(r.Id) ? r with { Evidence = new(r.Item, note.ToString(), now) } : r).ToArray());
        }
    }
    private void Update(Guid id, Func<Listing, Listing> update)
    { lock (sync) Save(rows.Select(r => r.Id == id ? update(r) : r).ToArray()); }
    private static string Clean(string value) => new(value.Where(c => !char.IsControl(c)).Take(256).ToArray());
    private static Listing[] Bound(Listing[] values) => values.TakeLast(MaxListings)
        .Select(r => r with { Item = Clean(r.Item), Base = Clean(r.Base), League = Clean(r.League), Prices = r.Prices.TakeLast(MaxPrices).ToArray() }).ToArray();
    private static void Validate(Listing[]? values)
    {
        if (values is null || values.Any(r => r is null || r.Item is null || r.Base is null || r.League is null ||
            r.Prices is null || r.Prices.Length == 0 || r.Prices.Any(p => p is null || p.Note is null ||
                p.Kind is null || ShopNote.Parse(p.Note.ToString()) is null)))
            throw new JsonException("Invalid shop history records.");
    }
    private void Save(Listing[] values)
    {
        if (unavailable) throw new IOException("Previous shop history could not be preserved.");
        var next = Bound(values);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(next);
        if (bytes.Length > MaxBytes) throw new IOException("Shop history exceeds size limit.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            File.Move(temp, path, true);
            rows = next;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
