using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Input;

public readonly record struct PhysicalKey(int Key, bool Down, bool Injected, KeyModifiers Modifiers, long Timestamp);
public sealed class PhysicalPress
{
    internal PhysicalPress(long timestamp, HotkeyBinding binding) { Timestamp = timestamp; Binding = binding; }
    private static long nextId;
    public long Id { get; } = Interlocked.Increment(ref nextId);
    internal int Redeemed;
    internal bool Executed;
    internal PhysicalPress? Origin { get; init; }
    public bool IsRelease => Origin is not null;
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
    private readonly PhysicalPress?[] pending = new PhysicalPress?[256];
    private readonly bool[] held = new bool[256];
    private readonly bool[] blocked = new bool[256];
    private readonly long[] lastDown = new long[256];
    // Hook-blocked downs do not update GetAsyncKeyState. Recover a missing release only
    // after hook-observed silence beyond the platform's repeat/acceptance timing plus margin.
    internal const long BlockedReleaseSilenceMilliseconds = 1200;
    private Func<long> releaseSilenceThreshold;
    internal void SetRepeatProvider(Func<long> provider) => releaseSilenceThreshold = provider;
    private Func<long, string, ActionKind, IntentToken>? issue;
    internal IntentRegistry Registry { get; }
    private int issuerInstalled;
    private void ClaimIssuer()
    {
        if (Interlocked.CompareExchange(ref issuerInstalled, 1, 0) != 0)
            throw new InvalidOperationException("Issuer already installed");
    }
    internal void InstallIssuer(Func<long, string, ActionKind, IntentToken> capability)
    { ArgumentNullException.ThrowIfNull(capability); ClaimIssuer(); issue = capability; }
    private Func<long, HotkeyBinding, IntentToken>? featureIssue;
    internal void InstallFeatureIssuer(Func<long, HotkeyBinding, IntentToken> capability)
    { ArgumentNullException.ThrowIfNull(capability); ClaimIssuer(); featureIssue = capability; }
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
    public HotkeyDispatcher(IntentRegistry registry, IEnumerable<HotkeyBinding> bindings,
        Func<long>? releaseSilenceThreshold = null)
    {
        Registry = registry;
        this.releaseSilenceThreshold = releaseSilenceThreshold ?? (() => BlockedReleaseSilenceMilliseconds);
        configuration = new HotkeyConfiguration();
        configuration.Replace(bindings, []);
        configuration.UpdateGameFocused(true);
    }
    public HotkeyDispatcher(IntentRegistry registry, HotkeyConfiguration configuration,
        Func<long>? releaseSilenceThreshold = null)
    {
        Registry = registry;
        this.releaseSilenceThreshold = releaseSilenceThreshold ?? (() => BlockedReleaseSilenceMilliseconds);
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

    internal bool IsTimingRelevant(PhysicalKey key) =>
        !key.Injected && (uint)key.Key < 256 && (uint)key.Modifiers < 16 &&
        (blocked[key.Key] || pending[key.Key] is not null || configuration.Match(key.Key, key.Modifiers) is not null);

    internal DispatchDecision Observe(PhysicalKey key, bool active, bool passThrough = false)
    {
        if (key.Injected || (uint)key.Key >= 256 || (uint)key.Modifiers >= 16) return default;
        if (passThrough)
        {
            foreach (var pendingPress in pending)
                if (pendingPress is not null) InputTiming.Record(InputStage.ReleaseCancelled, 0);
            Array.Clear(blocked); Array.Clear(pending); active = false;
        }
        if (!key.Down)
        {
            var block = blocked[key.Key];
            held[key.Key] = blocked[key.Key] = false;
            var original = pending[key.Key];
            pending[key.Key] = null;
            return new(block, original is null ? null : new PhysicalPress(key.Timestamp, original.Binding) { Origin = original });
        }
        var silence = key.Timestamp - lastDown[key.Key];
        if (held[key.Key] && blocked[key.Key] && silence > Math.Max(BlockedReleaseSilenceMilliseconds, releaseSilenceThreshold()))
        {
            if (pending[key.Key] is not null) InputTiming.Record(InputStage.ReleaseCancelled, 0);
            held[key.Key] = false; pending[key.Key] = null;
        }
        // Every real autorepeat extends the held interval, even when its context is inactive.
        lastDown[key.Key] = Math.Max(lastDown[key.Key], key.Timestamp);
        if (held[key.Key]) return new(blocked[key.Key], null);
        held[key.Key] = true;
        var binding = active ? configuration.Match(key.Key, key.Modifiers) : null;
        blocked[key.Key] = binding?.Block == true;
        var press = binding is null ? null : new PhysicalPress(key.Timestamp, binding);
        pending[key.Key] = binding?.TrackRelease == true ? press : null;
        return new(blocked[key.Key], press);
    }
    // Called only by the hook queue consumer, for a decision produced by Observe.
    internal IntentToken Issue(PhysicalPress press)
    {
        if (press.Origin is not null) throw new InvalidOperationException("A release cannot issue an intent");
        if (Interlocked.Exchange(ref press.Redeemed, 1) != 0) throw new InvalidOperationException("Press already issued");
        if (featureIssue is not null) return featureIssue(press.Timestamp, press.Binding);
        return (issue ?? throw new InvalidOperationException("Hook issuer not installed"))(press.Timestamp, press.Binding.Id, (press.Binding.Action ?? throw new InvalidOperationException("Feature commands cannot issue game tokens")).Kind);
    }
}
