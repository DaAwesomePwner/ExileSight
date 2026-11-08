// Provenance: Exile-UI modules/item-checker.ahk:865–1100; explicit profile confirmation avoids party-name inference.
namespace ExileSight.Features.GearCmp;

public sealed class GearModel
{
    private readonly object sync = new();
    private readonly GearStore store;
    private readonly List<string> candidates = [];
    private string? character;
    private Dictionary<GearSlot, EquippedGear> slots;
    public GearModel(GearStore store)
    {
        this.store = store;
        character = store.ReadActive();
        slots = store.Load(character);
    }
    public string? Character { get { lock (sync) return character; } }
    public string? Notice { get { lock (sync) return store.Notice; } }
    public string[] Candidates { get { lock (sync) return candidates.ToArray(); } }
    public void Observe(string name)
    {
        if (!GearStore.ValidName(name)) return;
        lock (sync)
        {
            if (name == character) return;
            candidates.Remove(name);
            candidates.Insert(0, name);
            if (candidates.Count > 5) candidates.RemoveAt(5);
        }
    }
    public void Confirm(string? name)
    {
        lock (sync)
        {
            if (name is not null && !candidates.Contains(name)) throw new InvalidOperationException("Character is not a current candidate");
            var loaded = store.Load(name);
            store.WriteActive(name);
            character = name;
            slots = loaded;
            if (name is not null) candidates.Remove(name);
        }
    }
    public bool Has(GearSlot slot) { lock (sync) return slots.ContainsKey(slot); }
    public EquippedGear[] Replaced(GearSlot slot, EquippedGear candidate)
    {
        lock (sync) return ReplacementSlots(slot, candidate).Select(key => slots[key]).ToArray();
    }
    // Capture slot identities and values under the same lock for structured compare headings.
    internal Dictionary<GearSlot, EquippedGear> ReplacedBySlot(GearSlot slot, EquippedGear candidate)
    {
        lock (sync) return ReplacementSlots(slot, candidate).ToDictionary(key => key, key => slots[key]);
    }
    // Both preview and mutation consume this exact set, including overwritten target slots.
    private GearSlot[] ReplacementSlots(GearSlot slot, EquippedGear gear)
    {
        var removed = new HashSet<GearSlot>();
        if (slots.ContainsKey(slot)) removed.Add(slot);
        if (slot == GearSlot.MainHand && slots.TryGetValue(GearSlot.OffHand, out var off) &&
            (gear.ItemClass == "Bows" ? off.ItemClass != "Quivers" :
                GearSlots.TwoHanded(gear.ItemClass) || off.ItemClass == "Quivers"))
            removed.Add(GearSlot.OffHand);
        if (slot == GearSlot.OffHand && slots.TryGetValue(GearSlot.MainHand, out var main) &&
            (gear.ItemClass == "Quivers" ? main.ItemClass != "Bows" : GearSlots.TwoHanded(main.ItemClass)))
            removed.Add(GearSlot.MainHand);
        return removed.ToArray();
    }
    public void Equip(string? expectedCharacter, GearSlot slot, EquippedGear gear)
    {
        lock (sync)
        {
            CheckCharacter(expectedCharacter);
            var next = new Dictionary<GearSlot, EquippedGear>(slots);
            foreach (var removed in ReplacementSlots(slot, gear)) next.Remove(removed);
            next[slot] = gear;
            store.Save(character, next);
            slots = next;
        }
    }
    public void Clear(string? expectedCharacter, GearSlot slot)
    {
        lock (sync)
        {
            CheckCharacter(expectedCharacter);
            var next = new Dictionary<GearSlot, EquippedGear>(slots);
            next.Remove(slot);
            store.Save(character, next);
            slots = next;
        }
    }
    private void CheckCharacter(string? expected)
    {
        if (character != expected) throw new InvalidOperationException("Character changed; reopen the menu");
    }
}
