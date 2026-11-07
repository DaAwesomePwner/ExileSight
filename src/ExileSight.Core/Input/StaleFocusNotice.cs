using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Input;

/// <summary>One coalesced diagnostic, published without locks from the hook and drained by its worker.</summary>
internal sealed class StaleFocusNotice
{
    internal const string Reason = "hotkey ignored: focus state stale";
    private HotkeyBinding? latest;
    internal bool RecordCandidate(HotkeyDispatcher dispatcher, PhysicalKey key)
    {
        if (!dispatcher.IsCandidate(key)) return false;
        Interlocked.Exchange(ref latest, dispatcher.Configuration.Match(key.Key, key.Modifiers));
        return true;
    }
    internal HotkeyBinding? Take() => Interlocked.Exchange(ref latest, null);
}
