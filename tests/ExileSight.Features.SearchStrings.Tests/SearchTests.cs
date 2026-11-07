using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.SearchStrings.Tests;

public sealed class SearchTests
{
    [Fact]
    public void EscapesLiteralMetacharactersAndCountsQuotesInLimit()
    {
        Assert.Equal("\"a\\+b\\(c\\)\"", SearchSettings.Compile(new("test", "a+b(c)"))[0]);
        Assert.Equal(250, SearchSettings.Compile(new("test", new string('a', 248)))[0].Length);
        Assert.Throws<ArgumentException>(() => SearchSettings.Compile(new("test", new string('+', 125))));
    }
    [Theory]
    [InlineData("")]
    [InlineData("a\tb")]
    [InlineData("a\"b")]
    [InlineData("a\n\nb")]
    public void RejectsUnsafeOrEmptyLines(string text) =>
        Assert.Throws<ArgumentException>(() => SearchSettings.Compile(new("test", text)));

    [Fact]
    public void UserVendorReplacesDefaultListIncludingEmptyLists()
    {
        var saved = new Dictionary<string, SavedSearch[]> { ["Vendor"] = [new("Mine", "custom")], ["Stash"] = [] };
        var merged = SearchSettings.Load(JsonSerializer.SerializeToElement(saved));
        Assert.Equal("Mine", Assert.Single(merged["Vendor"]).Name);
        Assert.Empty(merged["Stash"]);
        Assert.NotEmpty(SearchSettings.Load(null)["Vendor"]);
    }
    [Fact]
    public void OpenAndWheelNeverSendAndSelectionIsSingleUse()
    {
        var feature = new SearchStringsFeature();
        var calls = new List<GameAction>();
        var saved = JsonSerializer.SerializeToElement(new Dictionary<string, SavedSearch[]>
            { ["Vendor"] = [new("Cycle", "first\r\nsecond")] });
        var host = new FeatureHost([feature], (_, _) => saved, (_, _, _) => { },
            (_, _, action) => { calls.Add(action); return new(ActionOutcome.Success); });
        host.Commands.Single(c => c.Command == "searchstr:open").Execute();
        var menu = feature.Menu;
        Assert.Null(menu.Vendor);
        menu.ChooseVendor("Vendor");
        menu.Cycle(0, -1);
        Assert.Equal("\"second\"", menu.Text(0));
        menu.Cycle(0, 1);
        Assert.Equal("\"first\"", menu.Text(0));
        Assert.Empty(calls);
        menu.Prepare(0);
        var paste = host.Commands.Single(c => c.Command == "searchstr:paste");
        Assert.Equal(ActionOutcome.MissingToken, paste.Execute().Outcome);
        paste.Execute(new IntentToken());
        paste.Execute(new IntentToken());
        Assert.Equal("\"first\"", Assert.IsType<GameAction.PasteSearch>(Assert.Single(calls)).Text);
        Assert.Equal(ActionKind.PasteSearch, Assert.Single(feature.Manifest.GameActions));
        Assert.Equal(0x78, Assert.Single(host.DefaultBindings).Key);
    }
    [Fact]
    public void CancelAndReopenDiscardPreparedSearches()
    {
        var feature = new SearchStringsFeature();
        var calls = 0;
        var host = new FeatureHost([feature], (_, _) => null, (_, _, _) => { },
            (_, _, _) => { calls++; return new(ActionOutcome.Success); });
        var menu = feature.Menu;
        var paste = host.Commands.Single(c => c.Command == "searchstr:paste");
        menu.Open(); menu.ChooseVendor("Vendor"); menu.Prepare(0); menu.Cancel();
        paste.Execute(new IntentToken());
        menu.Prepare(0); menu.Open(); paste.Execute(new IntentToken());
        Assert.Equal(0, calls);
    }
    [Fact]
    public void RegexIsExplicitAndStillBounded()
    {
        Assert.Equal("\"a|b\"", SearchSettings.Compile(new("test", "a|b", true))[0]);
        Assert.Equal(250, SearchSettings.Compile(new("test", new string('a', 248), true))[0].Length);
        Assert.Throws<ArgumentException>(() => SearchSettings.Compile(new("test", new string('a', 249), true)));
    }
    [Theory]
    [InlineData("null")]
    [InlineData("{\"Vendor\":null}")]
    [InlineData("{\"Vendor\":[null]}")]
    [InlineData("{\"Vendor\":[{\"Name\":\"A\",\"Text\":\"x\"},{\"Name\":\"a\",\"Text\":\"y\"}]}")]
    public void InvalidListsAreRejected(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Throws<ArgumentException>(() => SearchSettings.Load(document.RootElement));
    }
    [Fact]
    public void InvalidSettingsFailClosedWithoutOverwritingSavedData()
    {
        var feature = new SearchStringsFeature();
        _ = new FeatureHost([feature], (_, _) => JsonSerializer.SerializeToElement("bad"),
            (_, _, _) => throw new Exception("Must not overwrite user data"));
        Assert.NotNull(feature.Menu.Notice);
        Assert.Empty(feature.Menu.Vendors);
    }
}
