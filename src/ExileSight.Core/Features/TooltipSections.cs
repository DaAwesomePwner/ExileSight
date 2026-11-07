using ExileSight.Core.Items;

namespace ExileSight.Core.Features;

public enum TooltipTone { Normal, Unknown, Low, Medium, High, Best, Crafted, Unique }
public sealed record TooltipText(string Text, TooltipTone Tone = TooltipTone.Normal);
public sealed record TooltipRow(IReadOnlyList<TooltipText> Text);
public sealed record TooltipSection(string Id, string Title, IReadOnlyList<TooltipRow> Rows);
public interface ITooltipSectionProvider
{
    int Priority { get; }
    TooltipSection? Build(ParsedItem item);
}
public static class TooltipSections
{
    // Stable registration order breaks ties. Smaller priorities appear first; the main item section reserves priority 100.
    public static IReadOnlyList<TooltipSection> Build(ParsedItem item, IEnumerable<ITooltipSectionProvider> providers) =>
        providers.OrderBy(p => p.Priority).Select(p => p.Build(item)).OfType<TooltipSection>().ToArray();
}
public static class TooltipDismissal
{
    public static bool MovedBeyond(double x, double y, double originX, double originY, double scale) =>
        Math.Pow(x - originX, 2) + Math.Pow(y - originY, 2) > Math.Pow(30 * scale, 2);
}
