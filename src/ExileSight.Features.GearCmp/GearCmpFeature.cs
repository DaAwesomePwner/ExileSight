// Provenance: Exile-UI modules/item-checker.ahk:865–1100,2354–2613; no pixel placement or game actions.
using System.Globalization;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Core.Logs;

namespace ExileSight.Features.GearCmp;

[DeclaredActions]
public sealed class GearCmpFeature(GearModel? suppliedModel) : IFeature, ITooltipSectionProvider, IItemMenuProvider
{
    public GearCmpFeature() : this(null) { }
    private GearModel Model => suppliedModel ?? throw new InvalidOperationException("Compose the gear model before registration.");
    public Func<ModLine, StatMatch[]> MatchLine { get; init; } = _ => [];
    public FeatureManifest Manifest { get; } = new("gearcmp", [Game.PoE1], [], ["poe1/stats"], [],
        "User-confirmed equipped gear and copied-item stat comparisons");
    public int Priority => 200;
    public void Register(IFeatureContext context)
    {
        _ = Model;
        context.Subscribe<CharacterLevelUp>(e => Model.Observe(e.Name));
        context.RegisterItemMenu(this);
        context.RegisterTooltipSection(this);
    }
    public IReadOnlyList<ItemMenuEntry> BuildMenu(ParsedItem item)
    {
        var slots = GearSlots.For(item);
        if (slots.Length == 0) return [];
        var character = Model.Character;
        var entries = new List<ItemMenuEntry>();
        foreach (var name in Model.Candidates)
            entries.Add(new("Use character: " + name, () => Model.Confirm(name)));
        if (character is not null) entries.Add(new("Use default profile", () => Model.Confirm(null)));
        var gear = GearStats.Read(item, MatchLine);
        foreach (var slot in slots)
        {
            var label = slots.Length == 1 ? "Set as equipped" : "Set as " + GearSlots.Label(slot);
            entries.Add(new(label + (character is null ? " (default profile)" : " (" + character + ")"),
                () => Model.Equip(character, slot, gear)));
            if (Model.Has(slot))
                entries.Add(new("Clear equipped " + GearSlots.Label(slot), () => Model.Clear(character, slot)));
        }
        return entries;
    }
    public TooltipSection? Build(ParsedItem item)
    {
        var slots = GearSlots.For(item);
        if (slots.Length == 0) return null;
        var rows = new List<TooltipRow>();
        rows.Add(new([new(Model.Character is { } name ? "Character: " + name :
            "Default profile — confirm a character in the long-press menu", TooltipTone.Unknown)]));
        if (Model.Notice is { } notice) rows.Add(new([new(notice, TooltipTone.Unknown)]));
        var candidate = GearStats.Read(item, MatchLine);
        foreach (var slot in slots)
        {
            var replaced = Model.Replaced(slot, candidate);
            var label = replaced.Length == 0 ? GearSlots.Label(slot) : string.Join(" + ", replaced.Select(g => g.ItemClass));
            rows.Add(new([new("Compared with " + label)]));
            if (replaced.Length == 0) { rows.Add(new([new("No equipped item", TooltipTone.Unknown)])); continue; }
            foreach (var (stat, delta) in GearStats.Delta(candidate, replaced).OrderBy(p => p.Key))
                rows.Add(new([new(stat + ": " + delta.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture),
                    delta > 0 ? TooltipTone.High : delta < 0 ? TooltipTone.Low : TooltipTone.Normal)]));
        }
        rows.Add(new([new("Known item stats only; DPS is copied weapon DPS, not character DPS.", TooltipTone.Unknown)]));
        return new("gearcmp", "Equipped comparison", rows);
    }
}
