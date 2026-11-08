using System.Text.Json;
using ExileSight.App.Features.SearchStrings;
using ExileSight.Core.Features;
using ExileSight.Features.SearchStrings;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Omni.Tests;

// Linked view-only helper keeps this regression coverage runnable without WPF.
public sealed class SearchSelectionViewTests
{
    [Fact]
    public void SwitchingVendorFromAnOpenListResetsLinesAndPreparedPaste()
    {
        var (menu, selection, host) = Create();
        selection.ChooseVendor("First");
        selection.Cycle(0, 1);
        menu.Prepare(0);
        Assert.Equal(2, selection.Line(0));

        // Same path used by the vendor chips while a list is already displayed.
        selection.ChooseVendor("Second");
        Assert.Equal("Second", menu.Vendor);
        Assert.Single(menu.Entries);
        Assert.Equal(1, selection.Line(0));
        Assert.Equal(2, selection.LineCount(0));
        Assert.Equal("\"delta\"", menu.Text(0));
        Assert.Equal(ActionOutcome.Failed, host.Commands.Single(c => c.Command == "searchstr:paste").Execute(new()).Outcome);

        selection.Cycle(0, -1);
        selection.ChooseVendor("First");
        Assert.Equal(1, selection.Line(0));
        Assert.Equal(3, selection.LineCount(0));
        Assert.Equal("\"alpha\"", menu.Text(0));
    }

    [Fact]
    public void DisplayedLineAgreesWithModelAcrossWrapsDuplicatesAndReselection()
    {
        var (menu, selection, _) = Create();
        selection.ChooseVendor("First");
        foreach (var direction in new[] { 1, 1, 1, -1, -1, -1, -1, 0, 20, -20 })
        {
            selection.Cycle(0, direction);
            var compiled = SearchSettings.Compile(menu.Entries[0]);
            Assert.InRange(selection.Line(0), 1, compiled.Length);
            Assert.Equal(compiled.Length, selection.LineCount(0));
            Assert.Equal(compiled[selection.Line(0) - 1], menu.Text(0));
            Assert.Equal(1, selection.Line(1)); // Other entries remain untouched.
        }
        selection.ChooseVendor("First");
        Assert.Equal(1, selection.Line(0));
        selection.Cycle(0, 1); // Identical text must still display the second line.
        Assert.Equal(2, selection.Line(0));
        Assert.Equal("\"alpha\"", menu.Text(0));
        selection.Cycle(1, -1);
        Assert.Equal(1, selection.Line(1));
    }

    private static (SearchMenu Menu, SearchStringsSelection Selection, FeatureHost Host) Create()
    {
        var feature = new SearchStringsFeature();
        var saved = JsonSerializer.SerializeToElement(new Dictionary<string, SavedSearch[]>
        {
            ["First"] = [new("Multi", "alpha\nalpha\nbeta"), new("Single", "gamma")],
            ["Second"] = [new("Other", "delta\nepsilon")]
        });
        var host = new FeatureHost([feature], (_, _) => saved, (_, _, _) => { },
            (_, _, _) => new(ActionOutcome.Success), dispatch: action => action());
        feature.Menu.Open();
        return (feature.Menu, new(feature.Menu), host);
    }
}
