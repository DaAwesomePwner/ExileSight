using System.Text.Json;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Core.Features;
using ExileSight.Features.Vaalasync;
using Xunit;

namespace ExileSight.Features.Vaalasync.Tests;

public sealed class RecoveryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "vaalasync-recovery-" + Guid.NewGuid());
    [Theory]
    [InlineData("truncated")]
    [InlineData("garbage")]
    [InlineData("null-prices")]
    [InlineData("null-item")]
    [InlineData("null-row")]
    [InlineData("null-root")]
    [InlineData("oversize")]
    public void BadHistoryIsPreservedAndDoesNotPreventRegistration(string corruption)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "history.json");
        var row = new Listing(Guid.NewGuid(), "Ring", "Iron Ring", 80, "Standard",
            [new(DateTimeOffset.UtcNow, new("~b/o", 2, "divine"), "Listed")]);
        var text = corruption switch
        {
            "truncated" => "[{\"Item\":", "garbage" => "this is not json",
            "null-prices" => JsonSerializer.Serialize(new[] { row with { Prices = null! } }),
            "null-item" => JsonSerializer.Serialize(new[] { row with { Item = null! } }),
            "null-row" => "[null]", "null-root" => "null", _ => "[]"
        };
        File.WriteAllText(path, text);
        if (corruption == "oversize")
        { using var file = File.OpenWrite(path); file.SetLength(HistoryStore.MaxBytes + 1); }
        var original = File.ReadAllBytes(path);
        var store = new HistoryStore(directory);
        Assert.Empty(store.Snapshot());
        var kept = Assert.Single(Directory.GetFiles(directory, "*.corrupt"));
        Assert.Equal(original, File.ReadAllBytes(kept));
        Assert.False(File.Exists(path));
        var model = new ShopModel(store, () => "Standard");
        var host = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { });
        Assert.Contains("History unavailable — previous file kept as " + kept, model.Status);
        var item = ItemTextParser.Parse("Item Class: Rings\nRarity: Normal\nIron Ring\n--------\nNote: ~b/o 2 divine", Game.PoE1).Item!;
        Assert.True(host.Route(item, new(Game.PoE1), OmniPressKind.Short));
        Assert.Contains(kept, model.Status);
        Assert.Single(new HistoryStore(directory).Snapshot());
        Assert.Equal(original, File.ReadAllBytes(kept));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedQuarantinePreservesOriginalAndDisablesWriting(bool accessDenied)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "history.json");
        const string original = "invalid json to preserve";
        File.WriteAllText(path, original);
        var attempts = 0;
        var store = new HistoryStore(directory, (source, destination) =>
        {
            attempts++;
            Assert.Equal(path, source);
            Assert.EndsWith(".corrupt", destination);
            if (accessDenied) throw new UnauthorizedAccessException("Injected quarantine refusal");
            throw new IOException("Injected quarantine failure");
        });
        Assert.Equal(1, attempts);
        Assert.Empty(store.Snapshot());
        Assert.Contains("previous file kept as " + path, store.RecoveryNotice);
        Assert.Empty(Directory.GetFiles(directory, "*.corrupt"));
        Assert.Throws<IOException>(() => store.RecordPurchase("Ring", "Standard", new("~b/o", 2, "divine"), DateTimeOffset.UtcNow));
        var model = new ShopModel(store, () => "Standard");
        var host = new FeatureHost([new VaalasyncFeature(model)], (_, _) => null, (_, _, _) => { });
        var item = ItemTextParser.Parse("Item Class: Rings\nRarity: Normal\nIron Ring\n--------\nNote: ~b/o 2 divine", Game.PoE1).Item!;
        Assert.True(host.Route(item, new(Game.PoE1), OmniPressKind.Short));
        Assert.False(model.CanApply);
        Assert.Null(model.Note);
        Assert.Contains("repricing disabled", model.Status);
        Assert.Equal(original, File.ReadAllText(path));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
