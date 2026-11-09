using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Items;

namespace ExileSight.Features.MapInfo;

public sealed class MapInfoModel(RatingStore store)
{
    private readonly object sync = new();
    private MapModsData? data;
    private MapMatcher matcher = new(new([]));
    private ParsedItem? last;
    private MapResult? current;
    private long? held;
    public string? Notice { get; private set; } = store.Notice;
    public MapResult? Current { get { lock (sync) return current; } }
    public event Action? Changed;
    public void Show(ParsedItem item, IPackReader packs)
    {
        lock (sync)
        {
            var next = packs.TryGet<MapModsData>("poe1/mapmods")?.Data;
            if (!ReferenceEquals(data, next)) { data = next; matcher = new(next ?? new([])); }
            last = item; held = null; Refresh();
        }
        Changed?.Invoke();
    }
    private void Refresh()
    {
        if (last is null) return;
        var result = matcher.Match(last);
        current = result with { Mods = result.Mods.Select(r => r with { Rank = r.Id is null ? null : store.Get(r.Id) })
            .OrderBy(MapCardModel.SortGroup).ToArray() };
    }
    public void Cycle(string id)
    {
        lock (sync)
        {
            try { store.Cycle(id); Notice = store.Notice; Refresh(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { Notice = store.Notice ?? "Could not save rating; previous rating retained."; }
        }
        Changed?.Invoke();
    }
    public void Hold(long id)
    {
        lock (sync) { if (last is null) return; held = id; Refresh(); }
        Changed?.Invoke();
    }
    public void Release(long id)
    {
        lock (sync) { if (held != id) return; held = null; current = null; }
        Changed?.Invoke();
    }
    public void Close()
    {
        lock (sync) { held = null; if (current is null) return; current = null; }
        Changed?.Invoke();
    }
}
