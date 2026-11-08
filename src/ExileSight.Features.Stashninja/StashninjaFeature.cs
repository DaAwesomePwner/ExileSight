using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Stashninja;

[DeclaredActions(ActionKind.PasteSearch)]
public sealed class StashninjaFeature : IFeature
{
    private readonly StashModel model;
    public StashninjaFeature() : this(new StashModel()) { }
    public StashninjaFeature(StashModel model) => this.model = model;
    public FeatureManifest Manifest { get; } = new("stashninja", [Game.PoE1], [ActionKind.PasteSearch], [],
        ["poe.ninja"], "Highlight stash matches on a separate physical click; price tags are display-only.", ["prices"],
        screenReads: ["automatic stash watch, page recognition and grid calibration (foreground game client, display-only)"]);
    public void Register(IFeatureContext context) => model.Register(context);
}
