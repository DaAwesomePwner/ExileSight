using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Input;

/// <summary>Writers build an immutable table off the hook path. A callback does one atomic read.
/// If contexts overlap, the first configured binding wins; one press can never fan out.</summary>
public sealed class HotkeyConfiguration : IHotkeyContexts
{
    private readonly object sync = new();
    private readonly Dictionary<string, bool> contexts = new(StringComparer.Ordinal) { ["gameFocused"] = false };
    private int generation;
    private nint foreground;
    internal int Generation => Volatile.Read(ref generation);
    public event Action? ContextRegistered;
    private HotkeyBinding[] bindings = [];
    private string[] rejected = [];
    private Snapshot snapshot = new(new HotkeyBinding?[256, 16], [], [], []);

    private sealed record Snapshot(HotkeyBinding?[,] Table, string[] Bindings, string[] Rejected, string[] Contexts);
    public IReadOnlyList<string> ActiveBindings => Array.AsReadOnly(Volatile.Read(ref snapshot).Bindings);
    public IReadOnlyList<string> RejectedBindings => Array.AsReadOnly(Volatile.Read(ref snapshot).Rejected);
    public IReadOnlyList<string> ActiveContexts => Array.AsReadOnly(Volatile.Read(ref snapshot).Contexts);
    public bool ContainsContext(string context) { lock (sync) return contexts.ContainsKey(context); }
    public void Register(string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        if (context == "gameFocused") throw new ArgumentException("Reserved context", nameof(context));
        bool added;
        lock (sync) added = contexts.TryAdd(context, false);
        if (added) ContextRegistered?.Invoke();
    }
    public void SetActive(string context, bool active)
    {
        if (context == "gameFocused") throw new ArgumentException("Reserved context", nameof(context));
        SetContext(context, active);
    }
    internal void UpdateGameFocused(bool active, nint window = default)
    {
        lock (sync)
        {
            if (foreground != window) { foreground = window; Interlocked.Increment(ref generation); }
            SetContext("gameFocused", active);
        }
    }
    private void SetContext(string context, bool active)
    {
        lock (sync)
        {
            if (!contexts.TryGetValue(context, out var previous)) throw new ArgumentException("Unknown context", nameof(context));
            if (previous == active) return;
            contexts[context] = active;
            Interlocked.Increment(ref generation);
            Publish();
        }
    }
    public void Replace(IEnumerable<HotkeyBinding> replacement, IEnumerable<string> rejections)
    {
        var next = replacement.ToArray();
        lock (sync)
        {
            foreach (var b in next)
                if (b.Key is < 1 or > 254 or 1 or 2 or 3 || (uint)b.Modifiers >= 16 || !contexts.ContainsKey(b.Context))
                    throw new ArgumentException("Invalid runtime binding", nameof(replacement));
            bindings = next;
            rejected = rejections.ToArray();
            Publish();
        }
    }
    internal HotkeyBinding? Match(int key, KeyModifiers modifiers) =>
        Volatile.Read(ref snapshot).Table[key, (int)modifiers];
    private void Publish()
    {
        var table = new HotkeyBinding?[256, 16];
        var warnings = new List<string>(rejected);
        foreach (var b in bindings)
        {
            if (!contexts[b.Context]) continue;
            if (table[b.Key, (int)b.Modifiers] is { } winner)
                warnings.Add($"{b.Id}: Shadowed by {winner.Id} in active context {winner.Context}");
            else table[b.Key, (int)b.Modifiers] = b;
        }
        Volatile.Write(ref snapshot, new(table,
            bindings.Select(b => $"{b.Id}: VK {b.Key:X2}, {b.Modifiers}, {b.Context}, {(b.Block ? "block" : "pass")}").ToArray(),
            warnings.ToArray(), contexts.Where(c => c.Value).Select(c => c.Key).ToArray()));
    }
}
