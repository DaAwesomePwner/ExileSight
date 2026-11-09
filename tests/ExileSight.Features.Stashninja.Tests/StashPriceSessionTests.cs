using ExileSight.Core.Features;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class StashPriceSessionTests
{
    [Fact]
    public void IdleDoesNotScheduleWorkAndVisitEndsOnClosure()
    {
        var session = new StashPriceSession();
        var scheduled = 0;
        void Schedule() { if (session.ShouldWatch(false)) scheduled++; }
        Schedule();
        Assert.Equal(0, scheduled);
        session.Toggle(false);
        Schedule();
        Assert.Equal(1, scheduled);
        session.EndVisit(); // Existing recognition reports closed/unknown.
        Schedule();
        Assert.Equal(1, scheduled);
        Assert.False(session.ShouldWatch(false));
    }

    [Fact]
    public void SecondPressStopsWorkAndNextPressStartsAgain()
    {
        var session = new StashPriceSession();
        session.Toggle(false);
        Assert.True(session.ShouldWatch(false));
        session.Toggle(false);
        Assert.False(session.ShouldWatch(false));
        session.Toggle(false);
        Assert.True(session.ShouldWatch(false));
    }

    [Fact]
    public void ExplicitAutomaticPreferenceSurvivesVisitAndCanBeHidden()
    {
        var session = new StashPriceSession();
        Assert.True(session.ShouldWatch(true));
        session.EndVisit();
        Assert.True(session.ShouldWatch(true));
        session.Toggle(true);
        Assert.False(session.ShouldWatch(true));
    }

    [Theory]
    [InlineData(false, false, false)] // Manual menu: preserve load and requested tags.
    [InlineData(false, true, true)] // On-demand visit: invalidate load and hide tags.
    [InlineData(true, false, true)] // Automatic watch: invalidate, then resume on foreground.
    [InlineData(true, true, false)] // Paused automatic watch: no active work to invalidate.
    public void ForegroundLossInvalidatesOnlyAnActiveWatch(bool automatic, bool press, bool invalidate)
    {
        var session = new StashPriceSession();
        if (press) session.Toggle(automatic);
        var watching = session.ShouldWatch(automatic);
        session.EndVisit();
        Assert.Equal(invalidate, watching);
        Assert.Equal(automatic, session.ShouldWatch(automatic));
    }

    [Fact]
    public void PricesCommandIsBlockedBindableAndDisplayOnly()
    {
        var model = new StashModel();
        var requests = 0;
        model.PricesRequested += () => requests++;
        var host = new FeatureHost([new StashninjaFeature(model)], (_, _) => null, (_, _, _) => { },
            execute: (_, _, _) => throw new InvalidOperationException("Display command must not invoke the gate"));
        var binding = Assert.Single(host.DefaultBindings, b => b.Command == "stashninja:prices");
        Assert.Equal(new FeatureDefaultBinding("stashninja.prices", 0x50,
            KeyModifiers.Control | KeyModifiers.Shift, true, "stashninja:prices"), binding);
        var command = Assert.Single(host.Commands, c => c.Command == "stashninja:prices");
        Assert.Null(command.RequiredAction);
        Assert.Equal(ActionOutcome.Success, command.Execute().Outcome);
        Assert.Equal(1, requests);
        Assert.False(model.AutomaticPrices);
    }
}
