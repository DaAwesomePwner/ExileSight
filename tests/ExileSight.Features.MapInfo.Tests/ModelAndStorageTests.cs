using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.MapInfo.Tests;

public sealed class ModelAndStorageTests
{
    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"schema\":2,\"ratings\":{\"012\":3}}")]
    [InlineData("{\"schema\":1,\"ratings\":{\"012\":4}}")]
    [InlineData("{\"schema\":1,\"ratings\":{},\"future\":true}")]
    public void InvalidOrFutureStoreIsPreserved(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "ratings.json"); File.WriteAllText(path, content);
            var store = new RatingStore(dir);
            Assert.NotNull(store.Notice);
            var model = new MapInfoModel(store);
            model.Cycle("012");
            Assert.Equal(store.Notice, model.Notice);
            Assert.Throws<IOException>(() => store.Cycle("012"));
            Assert.Equal(content, File.ReadAllText(path));
        }
        finally { Directory.Delete(dir, true); }
    }
    [Fact]
    public void OversizeStoreIsPreserved()
    {
        InvalidOrFutureStoreIsPreserved(new string(' ', RatingStore.MaxBytes + 1));
    }
    [Fact]
    public void OmniRoutingAndHoldReleaseKeepUnknownDistinct()
    {
        var model = new MapInfoModel(new RatingStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
        var handler = new MapInfoHandler(model, EmptyPackReader.Instance);
        var item = ItemTextParser.Parse("Item Class: Maps\nRarity: Rare\nDunes Map\n--------\nUnknown mod", Game.PoE1).Item!;
        Assert.True(handler.Priority > 100);
        Assert.True(handler.Matches(item, new(Game.PoE1)));
        model.Hold(1); Assert.Null(model.Current);
        handler.Handle(item, new(Game.PoE1), OmniPressKind.Short);
        Assert.Null(Assert.Single(model.Current!.Mods).Rank);
        Assert.True(model.Current.Mods[0].IsUnknown);
        model.Close(); model.Hold(2); Assert.NotNull(model.Current);
        model.Release(1); Assert.NotNull(model.Current);
        model.Release(2); Assert.Null(model.Current);
        model.Hold(3); handler.Handle(item, new(Game.PoE1), OmniPressKind.Short);
        model.Release(3); Assert.NotNull(model.Current);
        Assert.Empty(new MapInfoFeature().Manifest.GameActions);
    }
}
