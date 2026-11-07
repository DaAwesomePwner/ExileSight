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
        lock (sync)
        {
            if (GearSlots.TwoHanded(candidate.ItemClass) && candidate.ItemClass != "Bows") return slots
                .Where(p => p.Key is GearSlot.MainHand or GearSlot.OffHand).Select(p => p.Value).ToArray();
            if (candidate.ItemClass == "Quivers")
                return slots.TryGetValue(GearSlot.OffHand, out var quiver) && quiver.ItemClass == "Quivers" ? [quiver] : [];
            if (slot == GearSlot.OffHand && slots.TryGetValue(GearSlot.MainHand, out var main) &&
                GearSlots.TwoHanded(main.ItemClass)) return [main];
            return slots.TryGetValue(slot, out var gear) ? [gear] : [];
        }
    }
    public void Equip(string? expectedCharacter, GearSlot slot, EquippedGear gear)
    {
        lock (sync)
        {
            CheckCharacter(expectedCharacter);
            var next = new Dictionary<GearSlot, EquippedGear>(slots);
            if (slot == GearSlot.MainHand && next.TryGetValue(GearSlot.OffHand, out var off))
            {
                if (gear.ItemClass == "Bows" ? off.ItemClass != "Quivers" :
                    GearSlots.TwoHanded(gear.ItemClass) || off.ItemClass == "Quivers")
                    next.Remove(GearSlot.OffHand);
            }
            if (slot == GearSlot.OffHand && next.TryGetValue(GearSlot.MainHand, out var main))
            {
                if (gear.ItemClass == "Quivers" ? main.ItemClass != "Bows" : GearSlots.TwoHanded(main.ItemClass))
                    next.Remove(GearSlot.MainHand);
            }
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
