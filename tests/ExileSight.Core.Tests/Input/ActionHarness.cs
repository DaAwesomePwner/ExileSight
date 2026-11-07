using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Tests.Input;

internal sealed class ActionHarness
{
    public readonly TestClock Clock = new();
    public readonly TestClipboard Clipboard = new();
    public readonly TestSender Sender = new();
    public readonly TestAudit Audit = new();
    public readonly ActionExecutor Gate;
    private readonly IntentRegistry registry;
    public ActionHarness()
    {
        registry = new(Clock);
        Gate = new(new GatePolicy(registry, Enum.GetValues<ActionKind>()), Clipboard, Sender, Clock, Audit);
    }
    public IntentToken Token(GameAction action)
    {
        var dispatcher = new HotkeyDispatcher(registry, [new("test", 118, 0, true, action)]);
        return dispatcher.Issue(dispatcher.Observe(new(118, true, false, 0, Clock.Milliseconds), true).Press!);
    }
    public ActionResult Run(GameAction action) => Gate.TryExecute(Token(action), action);
}
internal sealed class TestClock : IClock
{
    public long Milliseconds { get; private set; } = 10000;
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch.AddMilliseconds(Milliseconds);
    public Action<int>? OnDelay { get; set; }
    public void Delay(int milliseconds) { Milliseconds += milliseconds; OnDelay?.Invoke(milliseconds); }
}
internal sealed class TestClipboard : IClipboard
{
    public string? Text { get; set; } = "private user clipboard";
    public uint SequenceNumber { get; private set; }
    public Dictionary<uint, byte[]> ExtraFormats { get; } = [];
    public uint[] SkippedFormats { get; set; } = [];
    public bool SaveFails { get; set; }
    public bool SetFails { get; set; }
    public bool ReadFails { get; set; }
    public int RestoreFailures { get; set; }
    public int RestoreCalls { get; private set; }
    public int ClearFailures { get; set; }
    public int ClearCalls { get; private set; }
    public Action? BeforeSet { get; set; }
    public ClipboardSnapshot Save() => SaveFails ? throw new IOException() :
        new(new Dictionary<uint, byte[]>(ExtraFormats) { [13] = System.Text.Encoding.Unicode.GetBytes(Text ?? "") }, SkippedFormats);
    public bool PasteConsumed { get; set; }
    public int ConsumeAfterPumps { get; set; } = 151;
    private int pumps;
    public void OfferText(string text) { SetText(text); PasteConsumed = false; pumps = 0; }
    public void Pump() { if (++pumps >= ConsumeAfterPumps) PasteConsumed = true; }
    public string? GetText() => ReadFails ? throw new IOException() : Text;
    public void SetText(string? text)
    {
        BeforeSet?.Invoke();
        if (SetFails) throw new IOException();
        if (text is null)
        {
            ClearCalls++;
            if (ClearFailures-- > 0) throw new IOException();
        }
        Text = text;
        ExtraFormats.Clear();
        SequenceNumber++;
    }
    public void Restore(ClipboardSnapshot? saved)
    {
        RestoreCalls++;
        if (RestoreFailures-- > 0) throw new IOException();
        ExtraFormats.Clear();
        if (saved is not null)
            foreach (var (format, bytes) in saved.Formats)
                if (format != 13) ExtraFormats.Add(format, bytes);
        Text = saved is null ? null : System.Text.Encoding.Unicode.GetString(saved.Formats[13]);
        SequenceNumber++;
    }
}
internal sealed class TestSender : IKeySender
{
    public bool IsGameForeground { get; set; } = true;
    public KeyModifiers Modifiers { get; set; }
    public KeySendResult Result { get; set; } = new(true, 4);
    public int Calls { get; private set; }
    public IReadOnlyList<KeyStroke>? LastSequence { get; private set; }
    public Action? OnSend { get; set; }
    public KeySendResult Send(IReadOnlyList<KeyStroke> sequence)
    {
        Calls++;
        LastSequence = sequence;
        OnSend?.Invoke();
        return Result;
    }
}
internal sealed class TestAudit : IActionAudit
{
    public string Path => "test audit";
    public bool PrepareFails { get; set; }
    public bool AppendFails { get; set; }
    public List<string> Lines { get; } = [];
    public void EnsureWritable() { if (PrepareFails) throw new IOException(); }
    public void Append(string line)
    {
        if (AppendFails) throw new IOException();
        Lines.Add(line);
    }
}
