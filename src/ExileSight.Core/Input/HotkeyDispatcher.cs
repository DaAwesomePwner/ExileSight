using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Input;

public readonly record struct PhysicalKey(int Key, bool Down, bool Injected, KeyModifiers Modifiers, long Timestamp);
public sealed class PhysicalPress
{
    internal PhysicalPress(long timestamp, HotkeyBinding binding) { Timestamp = timestamp; Binding = binding; }
    internal int Redeemed;
    public long Timestamp { get; }
    public HotkeyBinding Binding { get; }
}
public readonly record struct DispatchDecision(bool Block, PhysicalPress? Press)
{
    public HotkeyBinding? Binding => Press?.Binding;
}

/// <summary>Single hook-thread owner. Fixed lookup and state arrays keep callbacks constant time.</summary>
public sealed class HotkeyDispatcher
{
    private readonly HotkeyConfiguration configuration;
    private readonly bool[] held = new bool[256];
    private readonly bool[] blocked = new bool[256];
    private readonly long[] lastDown = new long[256];
    // Hook-blocked downs do not update GetAsyncKeyState. Recover a missing release only
    // after hook-observed silence beyond the maximum repeat delay plus a 200 ms margin.
    internal const long BlockedReleaseSilenceMilliseconds = 1200;
    private readonly IntentRegistry registry;
    private int generation;
    internal HotkeyConfiguration Configuration => configuration;
    // Run between message dispatches, never in a low-level callback. Async state is
    // evidence only for pass-through keys; blocked keys rely on hook events below.
    internal void ReconcileHeld(Func<int, bool> isDown)
    {
        var current = configuration.Generation;
        if (current == generation) return;
        generation = current;
        for (var key = 0; key < held.Length; key++)
            if (held[key] && !blocked[key] && !isDown(key)) held[key] = false;
    }
    public HotkeyDispatcher(IntentRegistry registry, IEnumerable<HotkeyBinding> bindings)
    {
        this.registry = registry;
        configuration = new HotkeyConfiguration();
        configuration.Replace(bindings, []);
        configuration.UpdateGameFocused(true);
    }
    public HotkeyDispatcher(IntentRegistry registry, HotkeyConfiguration configuration)
    {
        this.registry = registry;
        this.configuration = configuration;
    }
    /// <summary>Seed keys already down at hook installation; their repeats have no observed physical press.</summary>
    internal void SeedHeld(int key)
    {
        if ((uint)key >= 256) throw new ArgumentOutOfRangeException(nameof(key));
        held[key] = true;
    }
    internal bool IsCandidate(PhysicalKey key) =>
        !key.Injected && key.Down && (uint)key.Key < 256 && (uint)key.Modifiers < 16 &&
        !held[key.Key] && configuration.Match(key.Key, key.Modifiers) is not null;

    internal DispatchDecision Observe(PhysicalKey key, bool active)
    {
        if (key.Injected || (uint)key.Key >= 256 || (uint)key.Modifiers >= 16) return default;
        if (!key.Down)
        {
            var block = blocked[key.Key];
            held[key.Key] = blocked[key.Key] = false;
            return new(block, null);
        }
        var silence = key.Timestamp - lastDown[key.Key];
        if (held[key.Key] && blocked[key.Key] && silence > BlockedReleaseSilenceMilliseconds)
            held[key.Key] = false;
        // Every real autorepeat extends the held interval, even when its context is inactive.
        lastDown[key.Key] = Math.Max(lastDown[key.Key], key.Timestamp);
        if (held[key.Key]) return new(blocked[key.Key], null);
        held[key.Key] = true;
        var binding = active ? configuration.Match(key.Key, key.Modifiers) : null;
        blocked[key.Key] = binding?.Block == true;
        return new(blocked[key.Key], binding is null ? null : new PhysicalPress(key.Timestamp, binding));
    }
    // Called only by the hook queue consumer, for a decision produced by Observe.
    internal IntentToken Issue(PhysicalPress press)
    {
        if (Interlocked.Exchange(ref press.Redeemed, 1) != 0) throw new InvalidOperationException("Press already issued");
        return registry.Issue(press.Timestamp, press.Binding.Id, (press.Binding.Action ?? throw new InvalidOperationException("Feature commands cannot issue game tokens")).Kind);
    }
}
