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
        string[] shipped = ["currency/0", "currency/1", "essence/general"];
        var partial = new Dictionary<string, PageReferenceStore.Entry> { ["currency/0"] = refs["currency/0"] };
        Assert.Null(PageMatcher.MatchPage("currency", refs["currency/1"].SubSignature!, partial, shipped));
        Assert.Null(PageMatcher.MatchPage("currency", refs["currency/0"].SubSignature!, partial, shipped));
        Assert.False(PageMatcher.AllSiblingsTaught("currency", partial, shipped));
        partial["currency/1"] = refs["currency/1"] with { SubSignature = null };
        Assert.Null(PageMatcher.MatchPage("currency", refs["currency/1"].SubSignature!, partial, shipped));
        Assert.False(PageMatcher.AllSiblingsTaught("currency", partial, shipped));
        // An obsolete page must not substitute for a missing shipped page.
        partial["currency/retired"] = refs["currency/1"] with { Page = "currency/retired" };
        Assert.False(PageMatcher.AllSiblingsTaught("currency", partial, shipped));
        Assert.True(PageMatcher.AllSiblingsTaught("currency", refs, shipped));
        foreach (var page in new[] { "currency/0", "currency/1" })
        {
            Assert.Equal("currency", PageMatcher.MatchTab(refs[page].Signature, refs));
            Assert.Equal(page, PageMatcher.MatchPage("currency", refs[page].SubSignature!, refs, shipped));
        }
        Assert.Null(PageMatcher.MatchPage("currency", other, refs, shipped));
        refs["currency/1"] = refs["currency/0"] with { Page = "currency/1" };
        Assert.Null(PageMatcher.MatchPage("currency", refs["currency/0"].SubSignature!, refs, shipped));
        Assert.Equal("essence/general", PageMatcher.MatchPage("essence", null, refs, shipped));
    }
    [Theory]
    [InlineData(true, "currency", "currency", SubTabCaptureAction.Block)]
    [InlineData(true, "essence", "currency", SubTabCaptureAction.HideWrongTags)]
    [InlineData(true, "currency", null, SubTabCaptureAction.Block)]
    [InlineData(true, null, "currency", SubTabCaptureAction.Block)]
    [InlineData(false, "currency", "currency", SubTabCaptureAction.Sample)]
    public void SubTabOverlapDecisionKeepsOwnTags(bool overlaps, string? drawnTab, string? matchedTab, SubTabCaptureAction expected)
    {
        var state = new RecognitionState();
        for (var tick = 0; tick < 12; tick++)
        {
            Assert.Equal(expected, SubTabCapture.Decide(overlaps, drawnTab, matchedTab));
            if (expected != SubTabCaptureAction.Block) continue;
            Assert.Equal(RecognitionAction.None, state.Observe(null, "currency/0", captureSucceeded: false));
            Assert.False(state.Hidden); // Repeated blocked ticks cannot enter a hide/show cycle.
        }
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
