using ExileSight.Core.Features;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Features.Macros;

// Interaction derived from Exile-UI modules/macros.ahk and modules/GUI.ahk (MIT).
// No activation, sleeps or input synthesis are ported.
public sealed class MacroWheel
{
    private readonly object sync = new();
    private long version;
    private string[]? slots;
    public event Action<long, IReadOnlyList<string>>? Opened;
    public event Action? Closed;
    public Func<int, long, IntentToken, ActionResult> SelectSlot { get; internal set; } = (_, _, _) => new(ActionOutcome.Failed);
    public Func<MacroSettings> ReadSettings { get; internal set; } = () => MacroSettings.Default;
    public Action<MacroSettings>? SaveSettings { get; internal set; }
    public long Reset()
    {
        long result;
        lock (sync) { slots = null; result = ++version; }
        Closed?.Invoke();
        return result;
    }
    public bool IsCurrent(long id) { lock (sync) return id == version && slots is not null; }
    public void Show(long id, string[] commands)
    {
        lock (sync)
        {
            if (id != version) return;
            slots = (string[])commands.Clone();
        }
        Opened?.Invoke(id, Array.AsReadOnly((string[])commands.Clone()));
    }
    public ActionResult Select(int index, IntentToken? token, IFeatureContext context, long? expectedVersion = null)
    {
        string command;
        lock (sync)
        {
            if (token is null) return new(ActionOutcome.MissingToken);
            if ((expectedVersion is not null && expectedVersion != version) || slots is null || index < 0 || index >= 8 || string.IsNullOrWhiteSpace(slots[index]))
                return new(ActionOutcome.UsedOrUnknownToken);
            command = slots[index];
            slots = null;
            ++version;
        }
        Closed?.Invoke();
        return context.Execute(token, new GameAction.SendChatCommand(command));
    }
}
