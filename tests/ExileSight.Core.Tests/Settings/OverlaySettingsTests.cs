using System.Text.Json;
using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class OverlaySettingsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ExileSight-overlay-" + Guid.NewGuid());
    private JsonSettingsStore<SettingsDocument> Open() => new(new AppPaths(root),
        Path.Combine(root, "settings.json"), SettingsJsonContext.Default.SettingsDocument, () => new(),
        debounce: TimeSpan.FromHours(1));

    [Theory]
    [InlineData(.5)]
    [InlineData(2)]
    [InlineData(1)]
    public void OldDocumentDefaultsNewFieldsAndPreservesLegacyScale(double scale)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "settings.json"),
            JsonSerializer.Serialize(new { schemaVersion = 1, overlay = new { enabled = false, scaleOverride = scale } }));
        using var store = Open();
        Assert.False(store.LoadedDefaults);
        var overlay = store.Snapshot.Overlay;
        Assert.False(overlay.Enabled);
        Assert.Equal(scale, overlay.ScaleOverride);
        Assert.Equal(CardPlacementMode.BesideCursor, overlay.PlacementMode);
        Assert.Equal(OverlayDensity.Comfortable, overlay.Density);
        Assert.Equal(OverlayContrast.Standard, overlay.Contrast);
        Assert.Equal(1, store.Snapshot.SchemaVersion);
        store.Update("general", s => s.General.Language = "fr");
        store.Flush();
        Assert.Equal(scale, store.Snapshot.Overlay.ScaleOverride);
    }

    [Fact]
    public void PreferencesRoundTripThroughSourceGeneratedJson()
    {
        using (var store = Open())
            store.Update("overlay", s => s.Overlay = new()
            {
                PlacementMode = CardPlacementMode.Docked, DockedX = .2, DockedY = .8,
                Density = OverlayDensity.Compact, Contrast = OverlayContrast.HighContrast, ScaleOverride = 1.5
            });
        using var loaded = Open();
        var settings = loaded.Snapshot.Overlay;
        Assert.Equal(CardPlacementMode.Docked, settings.PlacementMode);
        Assert.Equal(.2, settings.DockedX);
        Assert.Equal(.8, settings.DockedY);
        Assert.Equal(OverlayDensity.Compact, settings.Density);
        Assert.Equal(OverlayContrast.HighContrast, settings.Contrast);
        Assert.Equal(1.5, settings.ScaleOverride);
    }

    [Theory]
    [InlineData(-.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidFractionsRejectUpdateWithoutPublishing(double fraction)
    {
        using var store = Open();
        Assert.Throws<JsonException>(() => store.Update("overlay", s => s.Overlay.DockedX = fraction));
        Assert.Throws<JsonException>(() => store.Update("overlay", s => s.Overlay.DockedY = fraction));
        Assert.Equal(.5, store.Snapshot.Overlay.DockedX);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidStoredScaleRejectsUpdate(double scale)
    {
        using var store = Open();
        Assert.Throws<JsonException>(() => store.Update("overlay", s => s.Overlay.ScaleOverride = scale));
    }

    [Theory]
    [InlineData(.79)]
    [InlineData(1.51)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void UiScaleRejectsOutsideSliderRange(double scale)
    {
        var overlay = new OverlaySettings();
        Assert.Throws<ArgumentOutOfRangeException>(() => overlay.SetUiScaleOverride(scale));
        Assert.Null(overlay.ScaleOverride);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(.8)]
    [InlineData(1.5)]
    public void UiScaleAcceptsAutoAndEndpoints(double? scale)
    {
        using var store = Open();
        store.Update("overlay", s => s.Overlay.SetUiScaleOverride(scale));
        Assert.Equal(scale, store.Snapshot.Overlay.ScaleOverride);
    }

    [Fact]
    public void InvalidEnumsRejectUpdate()
    {
        using var store = Open();
        Assert.Throws<JsonException>(() => store.Update("overlay", s => s.Overlay.PlacementMode = (CardPlacementMode)99));
        Assert.Throws<JsonException>(() => store.Update("overlay", s => s.Overlay.Density = (OverlayDensity)99));
        Assert.Throws<JsonException>(() => store.Update("overlay", s => s.Overlay.Contrast = (OverlayContrast)99));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
