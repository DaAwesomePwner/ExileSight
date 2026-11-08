using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Vaalasync;

[DeclaredActions(ActionKind.SetPriceNote, ActionKind.SetListingAmount)]
public sealed class VaalasyncFeature(ShopModel? model) : IFeature
{
    public VaalasyncFeature() : this(null) { }
    public FeatureManifest Manifest { get; } = new("vaalasync", [Game.PoE1], [ActionKind.SetPriceNote, ActionKind.SetListingAmount], [],
        ["poe.ninja"], "Paste one listing amount or price note per physical click; local sales history.", ["prices", "logs"]);
    public void Register(IFeatureContext context)
    {
        if (model is null) throw new InvalidOperationException("Compose the shop model before registration.");
        model.Register(context);
        context.RegisterOmniHandler(model);
        context.RegisterItemMenu(model);
        context.RegisterOverlay(model);
        context.Subscribe<TradeWhisper>(whisper => model.History.Evidence(whisper, DateTimeOffset.UtcNow));
    }
}
