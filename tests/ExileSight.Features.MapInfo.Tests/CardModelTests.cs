using System.Text.Json;
using Xunit;

namespace ExileSight.Features.MapInfo.Tests;

public sealed class CardModelTests
{
    [Fact]
    public void StatesSortStablyAndOnlyExplicitZeroFolds()
    {
        MapRow[] rows = [new("zero", "zero", null, 0), new("new", "new", null),
            new("two", "two", null, 2), new(null, "unknown", null), new("three", "three", null, 3),
            new("one", "one", null, 1), new("two-b", "two-b", null, 2)];
        var card = new MapCardModel(new("Map", 70, 39, null, rows), new());
        Assert.Equal(new string?[] { "three", null, "two", "two-b", "one", "new", "zero" }, card.Rows.Select(r => r.Id));
        Assert.Equal(1, card.Deadly); Assert.Equal(2, card.Risky);
        Assert.False(card.ShowFirstTimeHint);
        Assert.Equal("zero", Assert.Single(card.FoldedRows).Id);
        Assert.Contains(card.VisibleRows, r => r.Id == "new");
        Assert.Contains(card.VisibleRows, r => r.IsUnknown);
        Assert.True(card.QuantityGood); Assert.False(card.RarityGood); Assert.False(card.PackSizeGood);
        Assert.True(new MapCardModel(new("Map", null, null, null, [rows[1], rows[3]]), new()).ShowFirstTimeHint);
    }

    [Fact]
    public void ExistingRatingsRemainExplicitIncludingZero()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "ratings.json"), "{\"schema\":1,\"ratings\":{\"zero\":0,\"deadly\":3}}");
            var store = new RatingStore(dir);
            Assert.Equal(0, store.Get("zero")); Assert.Equal(3, store.Get("deadly")); Assert.Null(store.Get("new"));
            foreach (var rank in new[] { 1, 2, 3, 0, 1 }) Assert.Equal(rank, store.Cycle("new"));
            Assert.Equal(0, new RatingStore(dir).Get("zero"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ThresholdDefaultsAndValidation()
    {
        Assert.Equal(new MapInfoSettings(70, 40, 25), JsonSerializer.Deserialize<MapInfoSettings>("{}"));
        Assert.Equal(new MapInfoSettings(), MapInfoSettings.Read(null));
        Assert.Equal(new MapInfoSettings(), MapInfoSettings.Read(JsonSerializer.SerializeToElement(new { QuantityThreshold = -1 })));
        Assert.Equal(new MapInfoSettings(80, 50, 30), MapInfoSettings.Read(JsonSerializer.SerializeToElement(new MapInfoSettings(80, 50, 30))));
        Assert.Throws<ArgumentException>(() => new MapInfoSettings(-1).Validate());
        Assert.Throws<ArgumentException>(() => new MapInfoSettings(RarityThreshold: -1).Validate());
        Assert.Throws<ArgumentException>(() => new MapInfoSettings(PackSizeThreshold: -1).Validate());
        var card = new MapCardModel(new("Map", 70, 40, 25, []), new());
        Assert.True(card.QuantityGood && card.RarityGood && card.PackSizeGood);
    }
}
