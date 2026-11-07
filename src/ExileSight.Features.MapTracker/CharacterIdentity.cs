namespace ExileSight.Features.MapTracker;

/// <summary>Log-derived candidates only, following GearCmp/D-078. Never serialized.</summary>
internal sealed class CharacterIdentity
{
    private readonly List<string> candidates = [];
    public string? Confirmed { get; private set; }
    public string? Reconfirm { get; private set; }
    public string? Notice { get; private set; }
    public string[] Candidates => (Reconfirm is { } name ? new[] { name }.Concat(candidates) : candidates)
        .Distinct(StringComparer.Ordinal).Take(5).ToArray();
    public void Observe(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64 || name.Any(char.IsControl)) return;
        candidates.Remove(name); candidates.Insert(0, name);
        if (candidates.Count > 5) candidates.RemoveAt(5);
    }
    public bool Confirm(string? name)
    {
        if (name is null)
        { Clear("Character unconfirmed — confirm character again"); return true; }
        if (!Candidates.Contains(name, StringComparer.Ordinal)) return false;
        Confirmed = name; Reconfirm = null; Notice = null; return true;
    }
    public void Clear(string reason)
    {
        Reconfirm = Confirmed ?? Reconfirm;
        Confirmed = null; candidates.Clear(); Notice = reason;
    }
}
