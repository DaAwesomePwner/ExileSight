using System.Text.Json;
using Xunit;

namespace ExileSight.Features.SearchStrings.Tests;

public sealed class ValidationTests
{
    [Theory]
    [InlineData("a\u2028b")]
    [InlineData("a\u2029b")]
    [InlineData("a\u0085b")]
    [InlineData("a\vb")]
    [InlineData("a\fb")]
    public void RejectsEmbeddedLineSeparators(string text)
    {
        Assert.Throws<ArgumentException>(() => SearchSettings.Compile(new("test", text)));
        Assert.Throws<ArgumentException>(() => SearchSettings.Compile(new("test", text, true)));
    }
    [Theory]
    [InlineData("abc\\")]
    [InlineData("abc\\\\\\")]
    [InlineData("a\"b")]
    public void RejectsUnsafeRegexLines(string text) =>
        Assert.Throws<ArgumentException>(() => SearchSettings.Compile(new("test", text, true)));
    [Theory]
    [InlineData("abc\\\\")]
    [InlineData("abc\\\\\\\\")]
    public void AllowsEscapedTrailingBackslashes(string text) =>
        Assert.Equal("\"" + text + "\"", SearchSettings.Compile(new("test", text, true))[0]);
    [Fact]
    public void VendorLimitIncludesExactlyFiftyUserLists()
    {
        var lists = Enumerable.Range(0, 50).ToDictionary(i => "Vendor" + i, _ => Array.Empty<SavedSearch>());
        Assert.NotEmpty(SearchSettings.Load(JsonSerializer.SerializeToElement(lists)));
        lists.Add("One too many", []);
        Assert.Throws<ArgumentException>(() => SearchSettings.Load(JsonSerializer.SerializeToElement(lists)));
    }
    [Fact]
    public void EntryLimitIncludesExactlyOneHundred()
    {
        Dictionary<string, SavedSearch[]> Lists(int count) => new()
        { ["Vendor"] = Enumerable.Range(0, count).Select(i => new SavedSearch("Entry" + i, "text")).ToArray() };
        Assert.Equal(100, SearchSettings.Load(JsonSerializer.SerializeToElement(Lists(100)))["Vendor"].Length);
        Assert.Throws<ArgumentException>(() => SearchSettings.Load(JsonSerializer.SerializeToElement(Lists(101))));
    }
    [Fact]
    public void LineLimitIncludesExactlyOneHundred()
    {
        Assert.Equal(100, SearchSettings.Compile(new("test", string.Join('\n', Enumerable.Repeat("a", 100)))).Length);
        Assert.Throws<ArgumentException>(() => SearchSettings.Compile(new("test", string.Join('\n', Enumerable.Repeat("a", 101)))));
    }
    [Fact]
    public void NamesAllowEightyCharactersForVendorsAndSearches()
    {
        Dictionary<string, SavedSearch[]> Lists(int length) => new() { [new string('v', length)] = [] };
        Assert.NotEmpty(SearchSettings.Load(JsonSerializer.SerializeToElement(Lists(80))));
        Assert.Throws<ArgumentException>(() => SearchSettings.Load(JsonSerializer.SerializeToElement(Lists(81))));
        Assert.Single(SearchSettings.Compile(new(new string('n', 80), "text")));
        Assert.Throws<ArgumentException>(() => SearchSettings.Compile(new(new string('n', 81), "text")));
    }
}
