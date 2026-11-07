namespace ExileSight.Features.MapTracker;

/// <summary>Log-derived candidates only, following GearCmp/D-078. Never serialized.</summary>
internal sealed class CharacterIdentity
{
    private readonly List<string> candidates = [];
    public string? Confirmed { get; private set; }
    public string[] Candidates => candidates.ToArray();
    public void Observe(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64 || name.Any(char.IsControl)) return;
        candidates.Remove(name); candidates.Insert(0, name);
        if (candidates.Count > 5) candidates.RemoveAt(5);
    }
    public bool Confirm(string? name)
    {
        if (name is not null && !candidates.Contains(name, StringComparer.Ordinal)) return false;
        Confirmed = name; return true;
    }
    public void Clear() { Confirmed = null; candidates.Clear(); }
}
