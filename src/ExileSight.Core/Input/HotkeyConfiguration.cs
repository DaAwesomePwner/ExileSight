using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Input;

/// <summary>Writers build an immutable table off the hook path. A callback does one atomic read.
/// If contexts overlap, the first configured binding wins; one press can never fan out.</summary>
public sealed class HotkeyConfiguration : IHotkeyContexts
{
    private readonly object sync = new();
    private readonly Dictionary<string, bool> contexts = new(StringComparer.Ordinal) { ["gameFocused"] = false };
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
        lock (sync) contexts.TryAdd(context, false);
    }
    public void SetActive(string context, bool active)
    {
        lock (sync)
        {
            if (!contexts.TryGetValue(context, out var previous)) throw new ArgumentException("Unknown context", nameof(context));
            if (previous == active) return;
            contexts[context] = active;
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
        foreach (var b in bindings)
            if (contexts[b.Context]) table[b.Key, (int)b.Modifiers] ??= b;
        Volatile.Write(ref snapshot, new(table,
            bindings.Select(b => $"{b.Id}: VK {b.Key:X2}, {b.Modifiers}, {b.Context}, {(b.Block ? "block" : "pass")}").ToArray(),
            rejected, contexts.Where(c => c.Value).Select(c => c.Key).ToArray()));
    }
}
