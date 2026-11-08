using ExileSight.Core.Features;

using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Features.Vaalasync;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class ListingPresentationTests
{
    [Fact]
    public void ListingAgeUsesOriginalObservationAndOmitsTrimmedTimestamp()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var store = new HistoryStore(directory);
            var time = new DateTimeOffset(2026, 1, 2, 3, 0, 0, TimeSpan.Zero);
            var note = new ShopNote("~b/o", 40, "chaos");
            var id = store.Observe("Iron Ring", "Iron Ring", null, "Standard", note, time);
            var model = new ShopModel(store, () => "Standard");
            Assert.Null(model.FirstListedAt);
            var item = ItemTextParser.Parse("Item Class: Rings\nRarity: Normal\nIron Ring\n--------\nNote: ~b/o 40 chaos", Game.PoE1).Item!;
            model.Handle(item, new(Game.PoE1), OmniPressKind.Short);
            Assert.Equal("Iron Ring", model.CurrentItemName);
            Assert.Equal(time, model.FirstListedAt);
            store.Change(id, note, time.AddHours(1));
            Assert.Equal(time, model.FirstListedAt);
            for (var i = 0; i < HistoryStore.MaxPrices; i++) store.Change(id, note, time.AddHours(2));
            Assert.Null(model.FirstListedAt);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
