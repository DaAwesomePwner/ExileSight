namespace ExileSight.Features.Stashninja;

public static class PageMatcher
{
    public const double TabThreshold = .30, TabMargin = .08;
    public const double PageThreshold = .30, PageMargin = .06;
    private static string Tab(string page) => page.Split('/')[0];
    private static double Distance(double[] a, double[] b) => a.Zip(b).Average(p => Math.Abs(p.First - p.Second));
    public static string? MatchTab(double[] sample, IReadOnlyDictionary<string, PageReferenceStore.Entry> references)
    {
        if (!PageSignature.Valid(sample)) return null;
        // Sibling headers contribute evidence for one tab, never compete for its margin.
        var ranked = references.Where(p => PageSignature.Valid(p.Value.Signature))
            .GroupBy(p => Tab(p.Key)).Select(g => (Key: g.Key, Distance: g.Min(p => Distance(sample, p.Value.Signature))))
            .OrderBy(p => p.Distance).ToArray();
        return Winner(ranked, TabThreshold, TabMargin);
    }
    public static string? MatchPage(string tab, double[]? subSample,
        IReadOnlyDictionary<string, PageReferenceStore.Entry> references, bool hasSiblings)
    {
        var siblings = references.Where(p => Tab(p.Key) == tab).ToArray();
        if (!hasSiblings) return siblings.Length == 1 ? siblings[0].Key : null;
        if (!PageSignature.Valid(subSample)) return null;
        var ranked = siblings.Where(p => PageSignature.Valid(p.Value.SubSignature))
            .Select(p => (p.Key, Distance: Distance(subSample!, p.Value.SubSignature!))).OrderBy(p => p.Distance).ToArray();
        return Winner(ranked, PageThreshold, PageMargin);
    }
    private static string? Winner((string Key, double Distance)[] ranked, double threshold, double margin) =>
        ranked.Length > 0 && ranked[0].Distance < threshold &&
        (ranked.Length == 1 || ranked[1].Distance - ranked[0].Distance >= margin) ? ranked[0].Key : null;
}
