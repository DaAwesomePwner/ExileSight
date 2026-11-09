namespace ExileSight.Features.Stashninja;

/// <summary>Extra evidence required before a first-use wide search can supply an identity.</summary>
public static class GridWideAcceptance
{
    // The local corpus has supported wide scores >=18.40 versus admitted scarab
    // false candidates <=16.45. Keep the score floor below the weakest real band.
    public const double MinimumScore = GridIdentifier.MinimumScore + 2;
    public const double MaximumScaleDeviation = .01;

    public static bool Accepts(GridLocation candidate, Transform fit, double strongestOtherLocalScore) =>
        fit.IsValid && candidate.Transform is { IsValid: true } transform &&
        candidate.Score >= MinimumScore &&
        Math.Abs(transform.Scale / fit.Scale - 1) <= MaximumScaleDeviation + 1e-9 &&
        candidate.Score - strongestOtherLocalScore >= GridIdentifier.MinimumMargin;
}
