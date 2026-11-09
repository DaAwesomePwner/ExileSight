using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class GridWideAcceptanceTests
{
    private static GridLocation Candidate(double score, double scale = 1) => new(new(scale, 5, -5), 4, "Grid located", 1, score);

    [Fact]
    public void RejectsAdmittedScarabScoreButKeepsWeakRealBands()
    {
        var fit = new Transform(1, 0, 0);
        Assert.False(GridWideAcceptance.Accepts(Candidate(16.45), fit, 0));
        Assert.False(GridWideAcceptance.Accepts(Candidate(16.41), fit, 0));
        Assert.True(GridWideAcceptance.Accepts(Candidate(18.40), fit, 0));
        Assert.True(GridWideAcceptance.Accepts(Candidate(20), fit, 17));
        Assert.False(GridWideAcceptance.Accepts(Candidate(18.40), fit, 15.41));
        Assert.False(GridWideAcceptance.Accepts(Candidate(50) with { Transform = null }, fit, 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.72)]
    public void RequiresFitScaleAgreementEvenForStrongCandidates(double scale)
    {
        var fit = new Transform(scale, 0, 0);
        Assert.True(GridWideAcceptance.Accepts(Candidate(30, scale * .99), fit, 20));
        Assert.True(GridWideAcceptance.Accepts(Candidate(30, scale * 1.01), fit, 20));
        Assert.False(GridWideAcceptance.Accepts(Candidate(30, scale * .989), fit, 20));
        Assert.False(GridWideAcceptance.Accepts(Candidate(30, scale * 1.011), fit, 20));
        Assert.False(GridWideAcceptance.Accepts(Candidate(30, scale), fit, 27.01));
    }
}
