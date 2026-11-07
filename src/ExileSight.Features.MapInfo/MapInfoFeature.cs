using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.MapInfo;

[DeclaredActions]
public sealed class MapInfoFeature(MapInfoModel? model) : IFeature
{
    public MapInfoFeature() : this(null) { }
    public FeatureManifest Manifest { get; } = new("mapinfo", [Game.PoE1], [], ["poe1/mapmods"], [],
        "Copied map and logbook modifiers with local danger ratings. Optional mapinfo:hold binding; no game actions.");
    public void Register(IFeatureContext context)
    {
        var viewModel = model ?? throw new InvalidOperationException("Compose mapinfo storage before registration.");
        context.RegisterOverlay(viewModel);
        context.RegisterOmniHandler(new MapInfoHandler(viewModel, context.Packs));
        context.RegisterPressCommand("hold", null, (_, press) =>
        { viewModel.Hold(press.Id); return new(ActionOutcome.Success); }, release => viewModel.Release(release.Id));
    }
}

public sealed class MapInfoHandler(MapInfoModel model, IPackReader packs) : IOmniHandler
{
    public int Priority => 200;
    public bool Matches(ParsedItem item, OmniContext context) => context.Game == Game.PoE1 && item.Game == Game.PoE1 &&
        item.ItemClass is "Maps" or "Expedition Logbooks";
    public void Handle(ParsedItem item, OmniContext context, OmniPressKind press)
    { if (Matches(item, context)) model.Show(item, packs); }
}
