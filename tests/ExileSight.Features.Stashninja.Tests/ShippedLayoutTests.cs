using System.Text.Json;
using ExileSight.Features.Stashninja;
using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class ShippedLayoutTests
{
    private sealed record NameEvidence(string Page, string Category, string ItemName, string NinjaId);

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
            Assert.Equal(new Resolution(2000, 837), layout.ReferenceResolution);
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
    public void ShippedCellsHaveExactNamesInRecordedPoeNinjaEvidence()
    {
        var evidence = JsonSerializer.Deserialize<NameEvidence[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "measured-names.json")), Layouts.Json)!;
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        Assert.Equal(layouts.Sum(l => l.Cells.Length), evidence.Length);
        foreach (var layout in layouts)
        foreach (var cell in layout.Cells)
        {
            var page = layout.TabId is "currency" or "fragment"
                ? $"{layout.TabId}-{layout.Page}" : layout.TabId;
            var entry = Assert.Single(evidence, e => e.Page == page &&
                e.Category == cell.Category && e.ItemName == cell.ItemName);
            Assert.False(string.IsNullOrWhiteSpace(entry.NinjaId));
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
