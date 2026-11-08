using System.Text.Json;
using ExileSight.Core.Pricing;
using ExileSight.Features.MapTracker;
using Xunit;

namespace ExileSight.Features.MapTracker.Tests;

public sealed class LeagueChoicesTests
{
    private static LeagueResult Fixture()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "leagues.json")));
        var names = json.RootElement.GetProperty("economyLeagues").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()!).ToArray();
        return new(names, "Example Challenge", false, null);
    }

    [Fact]
    public void CurrentChallengeComesFirstAndHistoricalLeaguesAreNotOffered()
    {
        var choices = TrackerLeagueChoices.Build(Fixture(), "", "");
        Assert.Equal(new[] { "Example Challenge", "Example Ruthless", "Standard", "Hardcore" }, choices.Leagues);
        Assert.Equal("", choices.Text); // Discovery must not begin recording implicitly.
        Assert.Empty(choices.Note);
    }

    [Fact]
    public void SavedLeagueAndUncommittedTypedVariantSurviveDiscovery()
    {
        var choices = TrackerLeagueChoices.Build(Fixture(), "Saved SSF", "Typed Ruthless");
        Assert.Contains("Saved SSF", choices.Leagues);
        Assert.Equal("Typed Ruthless", choices.Text);
        var saved = TrackerLeagueChoices.Build(Fixture(), "Saved SSF", "Saved SSF");
        Assert.Equal("Saved SSF", saved.Text);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "offline")]
    public void UnverifiedListsFallBackWithoutPresentingOldChallengesAsCurrent(bool stale, string? diagnostic)
    {
        var choices = TrackerLeagueChoices.Build(Fixture() with { IsStale = stale, Diagnostic = diagnostic }, "Saved SSF", "typed");
        Assert.Equal(new[] { "Standard", "Hardcore", "Saved SSF" }, choices.Leagues);
        Assert.Equal("typed", choices.Text);
        Assert.NotEmpty(choices.Note);
    }

    [Fact]
    public void MissingOrEmptyListsHaveVisibleFallbackAndNoDuplicateSavedLeague()
    {
        foreach (var result in new LeagueResult?[] { null, new([], "Standard", false, null) })
        {
            var choices = TrackerLeagueChoices.Build(result, "Standard", "Standard");
            Assert.Equal(new[] { "Standard", "Hardcore" }, choices.Leagues);
            Assert.Equal("Standard", choices.Text);
            Assert.NotEmpty(choices.Note);
        }
    }
}
