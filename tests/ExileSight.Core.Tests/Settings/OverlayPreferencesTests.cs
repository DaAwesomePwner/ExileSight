using ExileSight.Core.Overlay;
using ExileSight.Core.Settings;
using Xunit;
namespace ExileSight.Core.Tests.Settings;

public sealed class OverlayPreferencesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "overlay-page-" + Guid.NewGuid());
    private JsonSettingsStore<SettingsDocument> Open() => new(new AppPaths(root), Path.Combine(root, "settings.json"),
        SettingsJsonContext.Default.SettingsDocument, () => new(), debounce: TimeSpan.FromHours(1));
    [Fact]
    public void ValidatedSaveClampsFractionsAndPreservesOtherFields()
    {
        using (var store = Open())
        {
            store.Update("overlay", d => new OverlayPreferences(CardPlacementMode.Docked, -1, 2, .8,
                OverlayDensity.Compact, OverlayContrast.HighContrast).Apply(d.Overlay));
            Assert.Equal(0, store.Snapshot.Overlay.DockedX);
            Assert.Equal(1, store.Snapshot.Overlay.DockedY);
        }
        using var reopened = Open();
        Assert.Equal(.8, reopened.Snapshot.Overlay.ScaleOverride);
        Assert.Equal(CardPlacementMode.Docked, reopened.Snapshot.Overlay.PlacementMode);
        Assert.True(reopened.Snapshot.Overlay.Enabled);
    }
    [Theory]
    [InlineData(.79, 0)]
    [InlineData(1.51, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(1, double.NaN)]
    public void InvalidInputNeverPublishes(double scale, double x)
    {
        using var store = Open();
        Assert.ThrowsAny<ArgumentException>(() => store.Update("overlay", d =>
            new OverlayPreferences(CardPlacementMode.Docked, x, 1, scale, OverlayDensity.Compact, OverlayContrast.Standard).Apply(d.Overlay)));
        Assert.Null(store.Snapshot.Overlay.ScaleOverride);
        Assert.Equal(.6, store.Snapshot.Overlay.DockedX);
        Assert.Equal(CardPlacementMode.BesideCursor, store.Snapshot.Overlay.PlacementMode);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
