using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Features.Stashninja;
using Xunit;

namespace ExileSight.Transparency.Tests;

public sealed class ScreenReadTests
{
    [Fact]
    public void ScreenReadsAreOptional() => Assert.Empty(Manifest().ScreenReads);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void ScreenReadPurposesMustBeNonempty(string? purpose) =>
        Assert.Throws<ArgumentException>(() => Manifest([purpose!]));

    [Fact]
    public void ScreenReadsAreAnImmutableDistinctSnapshot()
    {
        var purposes = new List<string> { "page recognition", "page recognition" };
        var manifest = Manifest(purposes);
        purposes.Clear();
        Assert.Equal("page recognition", Assert.Single(manifest.ScreenReads));
        manifest.Validate();
    }

    [Fact]
    public void StashninjaDeclaresDisplayOnlyPageRecognition()
    {
        var manifest = new StashninjaFeature().Manifest;
        Assert.Equal("stash page recognition and grid calibration (game client area, display-only)", Assert.Single(manifest.ScreenReads));
    }

    [Fact]
    public void GeneratedTableShowsScreenReadsAndExplicitAbsence()
    {
        var root = TransparencyTests.FindRoot();
        var scratch = Path.Combine(Path.GetTempPath(), "ExileSight-screen-reads-" + Guid.NewGuid().ToString("N"));
        try
        {
            var host = FeatureInventory.Read(root, scratch);
            var table = TransparencyTable.Generate(host, root);
            Assert.Contains("| Feature | Game actions | Screen reads |", table);
            Assert.Contains("| stashninja | `PasteSearch` | `stash page recognition and grid calibration (game client area, display-only)` |", table);
            Assert.Contains("| omni | `CopyItem` | None |", table);
            Assert.False(Directory.Exists(scratch));
        }
        finally { if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true); }
    }

    private static FeatureManifest Manifest(IEnumerable<string>? screenReads = null) =>
        new("probe", [Game.PoE1], [], [], [], "Screen read declaration test", screenReads: screenReads);
}
