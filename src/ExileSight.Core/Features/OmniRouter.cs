using ExileSight.Core.Items;

namespace ExileSight.Core.Features;

/// <summary>Stable ordering: registration order wins equal priorities. Handlers receive no token.</summary>
public sealed class OmniRouter(IEnumerable<IOmniHandler> handlers)
{
    private readonly IOmniHandler[] ordered = handlers.OrderByDescending(h => h.Priority).ToArray();
    public bool Route(ParsedItem item, OmniContext context, OmniPressKind press = OmniPressKind.Short)
    {
        var handler = ordered.FirstOrDefault(h => h.Matches(item, context));
        if (handler is null) return false;
        handler.Handle(item, context, press);
        return true;
    }
}
