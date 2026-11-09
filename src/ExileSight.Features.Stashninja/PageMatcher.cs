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
    private static string[] ShippedSiblings(string tab, IReadOnlyCollection<string> shippedPages) =>
        shippedPages.Where(p => Tab(p) == tab).Distinct(StringComparer.Ordinal).ToArray();
    public static bool AllSiblingsTaught(string tab, IReadOnlyDictionary<string, PageReferenceStore.Entry> references,
        IReadOnlyCollection<string> shippedPages)
    {
        var required = ShippedSiblings(tab, shippedPages);
        return required.Length > 1 && required.All(page => references.TryGetValue(page, out var entry) &&
            PageSignature.Valid(entry.Signature) && PageSignature.Valid(entry.SubSignature));
    }
    public static string? MatchPage(string tab, double[]? subSample,
        IReadOnlyDictionary<string, PageReferenceStore.Entry> references, IReadOnlyCollection<string> shippedPages)
    {
        var required = ShippedSiblings(tab, shippedPages);
        if (required.Length == 1)
            return references.TryGetValue(required[0], out var entry) && PageSignature.Valid(entry.Signature) ? required[0] : null;
        // A threshold alone cannot distinguish an untaught sibling. Require the
        // exact shipped set, not a count that obsolete/unknown references could fill.
        if (!AllSiblingsTaught(tab, references, shippedPages) || !PageSignature.Valid(subSample)) return null;
        var ranked = required.Select(page => (Key: page, Distance: Distance(subSample!, references[page].SubSignature!)))
            .OrderBy(p => p.Distance).ToArray();
        return Winner(ranked, PageThreshold, PageMargin);
    }
    private static string? Winner((string Key, double Distance)[] ranked, double threshold, double margin) =>
        ranked.Length > 0 && ranked[0].Distance < threshold &&
        (ranked.Length == 1 || ranked[1].Distance - ranked[0].Distance >= margin) ? ranked[0].Key : null;
}
