using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Items;

namespace ExileSight.Features.GearCmp;

public static class GearCardBuilder
{
    public static ItemCardComparison? Build(ParsedItem item, GearModel model, Func<ModLine, StatMatch[]> match)
    {
        var slots = GearSlots.For(item);
        if (slots.Length == 0 || item.Unidentified) return null;
        var candidate = GearStats.Read(item, match);
        var blocks = slots.Select(slot =>
        {
            var replaced = model.ReplacedBySlot(slot, candidate);
            var deltas = replaced.Count == 0 ? [] : GearStats.Delta(candidate, replaced.Values).OrderBy(p => p.Key)
                .Select(p => new ItemCardDelta(p.Key, p.Value > 0 ? DeltaDirection.Increase :
                    p.Value < 0 ? DeltaDirection.Decrease : DeltaDirection.Unchanged, Math.Abs(p.Value))).ToArray();
            var label = replaced.ContainsKey(GearSlot.MainHand) && replaced.ContainsKey(GearSlot.OffHand)
                ? "main + off hand" : GearSlots.Label(slot);
            return new ItemCardCompareSlot(label, replaced.Count == 0, deltas);
        }).ToArray();
        var character = model.Character is { } name ? "Character: " + name :
            "Default profile — confirm a character in the long-press menu";
        var note = string.Join(" · ", new[] { character, model.Notice,
            "Known item stats only; DPS is copied weapon DPS, not character DPS." }.Where(s => s is not null));
        return new(blocks, note);
    }
}
