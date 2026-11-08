using System.Text.Json;
using ExileSight.Features.Stashninja;
using ExileSight.Core.Pricing;
using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class ShippedLayoutTests
{
    private sealed record NameEvidence(string Page, string Category, string ItemName, string? NinjaId,
        string Confidence, string? ReferenceIcon, string? NoReferenceIcon);

    [Fact]
    public void EveryShippedLayoutLoadsWithUniquePagesAndNonOverlappingCells()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "layouts");
        var files = Directory.GetFiles(directory, "*.layout.json");
        Assert.NotEmpty(files);
        var layouts = Layouts.LoadDirectory(directory);
        Assert.Equal(files.Length, layouts.Length);
        Assert.Equal(layouts.Length, layouts.Select(l => (l.TabId, l.Page)).Distinct().Count());
        foreach (var layout in layouts)
        {
            for (var i = 0; i < layout.Cells.Length; i++)
            for (var j = i + 1; j < layout.Cells.Length; j++)
            {
                var a = layout.Cells[i]; var b = layout.Cells[j];
                var ao = Layouts.Origin(layout, a); var bo = Layouts.Origin(layout, b);
                Assert.False(ao.X < bo.X + b.W && bo.X < ao.X + a.W &&
                    ao.Y < bo.Y + b.H && bo.Y < ao.Y + a.H,
                    $"{layout.TabId}/{layout.Page}: {a.Id} overlaps {b.Id}");
            }
        }
    }

    [Fact]
    public void ShippedCellsHaveIndependentIdentityEvidence()
    {
        var evidence = JsonSerializer.Deserialize<NameEvidence[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "measured-names.json")), Layouts.Json)!;
        var directory = Path.Combine(AppContext.BaseDirectory, "layouts");
        var layouts = Directory.GetFiles(directory, "*.layout.json").ToDictionary(
            path => Path.GetFileName(path)[..^".layout.json".Length],
            path => Layouts.Parse(File.ReadAllText(path)));
        Assert.Equal(layouts.Keys.Order(), evidence.Select(e => e.Page).Distinct().Order());
        Assert.Equal(layouts.Values.Sum(l => l.Cells.Length), evidence.Length);
        foreach (var (page, layout) in layouts)
        foreach (var cell in layout.Cells)
        {
            var entry = Assert.Single(evidence, e => e.Page == page &&
                e.Category == cell.Category && e.ItemName == cell.ItemName);
            Assert.Contains(entry.Confidence, new[] { "high", "confirmed" });
            var hasReference = !string.IsNullOrWhiteSpace(entry.ReferenceIcon);
            var hasReason = !string.IsNullOrWhiteSpace(entry.NoReferenceIcon);
            Assert.True(hasReference ^ hasReason,
                $"{page}/{cell.Id} needs exactly one referenceIcon or noReferenceIcon reason");
            if (hasReference)
            {
                Assert.StartsWith("https://github.com/", entry.ReferenceIcon);
                Assert.Contains("/blob/", entry.ReferenceIcon);
            }
        }
    }

    [Fact]
    public void MissingPriceOmitsOnlyTheTagNotTheMeasuredCell()
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        Assert.NotEmpty(layouts);
        foreach (var layout in layouts)
        {
            var originalCells = layout.Cells.ToArray();
            var priced = layout.Cells[0];
            var prices = new PriceOverview([new(priced.NinjaId ?? priced.Id,
                priced.ItemName!, priced.Category, null, 1, null, [], null, null)], null);
            var tags = Tags.Build(layout, prices, new("test", 0, 10, "#FFFFFF", "#FFD700"));
            Assert.Equal(priced, Assert.Single(tags).Cell);
            Assert.Equal(originalCells, layout.Cells);
        }
    }

    [Fact]
    public void AllflameCategoryIsAcceptedForFutureMeasuredPages()
    {
        var fixture = Layouts.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "synthetic.json")));
        var json = JsonSerializer.Serialize(fixture with
        {
            Cells = fixture.Cells.Select(c => c with { Category = "AllflameEmber" }).ToArray()
        }, Layouts.Json);
        Assert.All(Layouts.Parse(json).Cells, c => Assert.Equal("AllflameEmber", c.Category));
    }
}
