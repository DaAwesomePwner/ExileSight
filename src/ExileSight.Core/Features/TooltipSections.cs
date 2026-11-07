using ExileSight.Core.Items;

namespace ExileSight.Core.Features;

public enum TooltipTone { Normal, Unknown, Low, Medium, High, Best, Crafted, Unique }
public sealed record TooltipText(string Text, TooltipTone Tone = TooltipTone.Normal);
public sealed record TooltipRow(IReadOnlyList<TooltipText> Text);
public sealed record TooltipSection(string Id, string Title, IReadOnlyList<TooltipRow> Rows);
public sealed record TooltipContext(ParsedItem Item, object? Analysis = null);
public interface ITooltipSectionProvider
{
    int Priority { get; }
    TooltipSection? Build(ParsedItem item);
    TooltipSection? Build(TooltipContext context) => Build(context.Item);
}
public static class TooltipSections
{
    // Stable registration order breaks ties. Smaller priorities appear first; the main item section reserves priority 100.
    public static IReadOnlyList<TooltipSection> Build(ParsedItem item, IEnumerable<ITooltipSectionProvider> providers) =>
        Build(new TooltipContext(item), providers);
    public static IReadOnlyList<TooltipSection> Build(TooltipContext context,
        IEnumerable<ITooltipSectionProvider> providers, Action<Exception>? report = null)
    {
        var sections = new List<(int Priority, TooltipSection Section)>();
        foreach (var provider in providers)
        {
            try
            {
                var priority = provider.Priority;
                if (provider.Build(context) is { } section) sections.Add((priority, section));
            }
            catch (Exception error) { report?.Invoke(error); }
        }
        return sections.OrderBy(s => s.Priority).Select(s => s.Section).ToArray();
    }
}
public static class TooltipDismissal
{
    public static bool MovedBeyond(double x, double y, double originX, double originY, double scale) =>
        Math.Pow(x - originX, 2) + Math.Pow(y - originY, 2) > Math.Pow(30 * scale, 2);
}
