using System.Runtime.InteropServices;
using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ExileSight.Platform.Windows.Input;

/// <summary>The sole native input adapter. Its sender is private and cannot bypass the token policy.</summary>
public sealed class ActionGate : IActionGate
{
    private readonly ActionExecutor executor;
    public ActionGate(GatePolicy policy, IClipboard clipboard, IClock clock) =>
        executor = new(policy, clipboard, new NativeSender(), clock, new FileActionAudit());
    public int ActionsSent => executor.ActionsSent;
    public string AuditPath => executor.AuditPath;
    public ActionResult TryExecute(IntentToken? token, GameAction action) => executor.TryExecute(token, action);

    private sealed class NativeSender : IKeySender
    {
        public bool IsGameForeground => GameForeground.IsGame();
        public KeyModifiers Modifiers => HookHost.Modifiers();
        public unsafe KeySendResult Send(IReadOnlyList<KeyStroke> sequence)
        {
            var inputs = new INPUT[sequence.Count];
            for (var i = 0; i < inputs.Length; i++)
            {
                var scan = PInvoke.MapVirtualKey(sequence[i].Key, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX);
                if (scan == 0) return new(false, 0, "Scan code mapping failed");
                inputs[i].type = INPUT_TYPE.INPUT_KEYBOARD;
                inputs[i].Anonymous.ki.wScan = (ushort)(scan & 0xff);
                inputs[i].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE |
                    (sequence[i].Down ? 0 : KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP) |
                    ((scan & 0xff00) != 0 ? KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY : 0);
            }
            uint sent;
            uint error;
            fixed (INPUT* pointer = inputs)
            {
#pragma warning disable RS0030
                sent = PInvoke.SendInput((uint)inputs.Length, pointer, sizeof(INPUT));
#pragma warning restore RS0030
                error = (uint)Marshal.GetLastWin32Error();
            }
            if (sent == inputs.Length) return new(true, sent);
            // Cleanup is only key-up, never another action. Include every down key even on zero/UIPI insertion.
            var releases = ActionData.ReleaseSequence(sequence);
            var cleanup = new INPUT[releases.Count];
            for (var i = 0; i < cleanup.Length; i++)
            {
                var source = sequence.Select((key, index) => (key, index))
                    .First(pair => pair.key.Key == releases[i].Key && pair.key.Down).index;
                cleanup[i] = inputs[source];
                cleanup[i].Anonymous.ki.dwFlags |= KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
            }
            uint released;
            fixed (INPUT* pointer = cleanup)
            {
#pragma warning disable RS0030
                released = PInvoke.SendInput((uint)cleanup.Length, pointer, sizeof(INPUT));
#pragma warning restore RS0030
            }
            return new(false, sent, $"SendInput inserted {sent}/{inputs.Length}; error {error}; cleanup {released}/{cleanup.Length}");
        }
    }
}
