using System.Text.Json;
using ExileSight.Features.Stashninja;
using Xunit;
namespace ExileSight.Features.Stashninja.Tests;

public sealed class TwoStageRecognitionTests
{
    private sealed record Fixture(int Width, int Height, int SubTop, int SubHeight,
        int ButtonTop, int ButtonHeight, int ButtonWidth, int[] Buttons, int ButtonContrast);
    [Fact]
    public void ButtonOnlyDifferencesResolveSiblingsWithoutCompetingAtTabStage()
    {
        var f = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "sibling-pages.json")), Layouts.Json)!;
        var refs = new Dictionary<string, PageReferenceStore.Entry>();
        for (var page = 0; page < 2; page++)
        {
            var bytes = new byte[f.Width * f.Height * 4];
            for (var y = 0; y < f.Height; y++)
            for (var x = 0; x < f.Width; x++)
            for (var c = 0; c < 3; c++)
                bytes[(y * f.Width + x) * 4 + c] = (byte)(40 + (x / 20 + y / 10) % 3 * 20 +
                    (x >= f.Buttons[page] && x < f.Buttons[page] + f.ButtonWidth &&
                     y >= f.ButtonTop && y < f.ButtonTop + f.ButtonHeight ? f.ButtonContrast : 0));
            var header = PageSignature.Create(f.Width, 90, bytes[..(f.Width * 90 * 4)]);
            var sub = PageSignature.Create(f.Width, f.SubHeight, bytes[(f.Width * f.SubTop * 4)..]);
            var key = "currency/" + page;
            refs[key] = new(new(2000, 837), key, header, sub);
        }
        // Reproduce the original failure: header-only sibling distance is about .068,
        // below the old .08 margin, although the full button row is distinguishable.
        Assert.Null(PageSignature.Match(refs["currency/0"].Signature,
            refs.ToDictionary(p => p.Key, p => p.Value.Signature)));
        var other = refs.First().Value.Signature.Select(v => -v).ToArray();
        refs["essence/general"] = new(new(2000, 837), "essence/general", other, null);
        foreach (var page in new[] { "currency/0", "currency/1" })
        {
            Assert.Equal("currency", PageMatcher.MatchTab(refs[page].Signature, refs));
            Assert.Equal(page, PageMatcher.MatchPage("currency", refs[page].SubSignature!, refs, true));
        }
        Assert.Null(PageMatcher.MatchPage("currency", other, refs, true));
        refs["currency/1"] = refs["currency/0"] with { Page = "currency/1" };
        Assert.Null(PageMatcher.MatchPage("currency", refs["currency/0"].SubSignature!, refs, true));
        Assert.Equal("essence/general", PageMatcher.MatchPage("essence", null, refs, false));
    }
    [Theory]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, true, true)]
    public void TeachingRespectsExplicitAutoDetectPreference(bool explicitTeach, bool preference, bool enabled, bool expected) =>
        Assert.Equal(expected, RecognitionState.EnableAfterTeach(explicitTeach, preference, enabled));

    [Fact]
    public void StableMatchesDoNotRefreshStatusAndFailedSamplesDoNotHide()
    {
        var state = new RecognitionState();
        Assert.Equal(RecognitionAction.None, state.Observe("page", "page"));
        Assert.Equal(RecognitionAction.None, state.Observe("page", "page"));
        Assert.Equal(RecognitionAction.None, state.Observe(null, "page"));
        Assert.Equal(RecognitionAction.None, state.Observe(null, "page"));
        for (var i = 0; i < 5; i++) Assert.Equal(RecognitionAction.None, state.Observe(null, "page", false));
        Assert.False(state.Hidden);
        state.Observe(null, "page"); state.Observe(null, "page");
        Assert.Equal(RecognitionAction.Hide, state.Observe(null, "page"));
        Assert.Equal(RecognitionAction.None, state.Observe("page", "page"));
        Assert.Equal(RecognitionAction.Restore, state.Observe("page", "page"));
        Assert.Equal(RecognitionAction.None, state.Observe("page", "page"));
        state.Observe("other", "page");
        Assert.Equal(RecognitionAction.Select, state.Observe("other", "page"));
    }
}
