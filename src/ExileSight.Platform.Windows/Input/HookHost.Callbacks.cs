using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ExileSight.Platform.Windows.Input;

public sealed partial class HookHost
{
    private unsafe LRESULT Keyboard(int code, WPARAM message, LPARAM data)
    {
        if (code >= 0)
        {
            var key = *(KBDLLHOOKSTRUCT*)data.Value;
            if (!NativeHookEvent.KeyboardAllowed((uint)message.Value, (uint)key.flags))
                return PInvoke.CallNextHookEx(default, code, message, data);
            // Hook timestamps share the system tick epoch; unsigned subtraction handles 32-bit wrap.
            var now = clock.Milliseconds;
            var timestamp = now - unchecked((uint)now - key.time);
            var physical = new PhysicalKey((int)key.vkCode,
                message.Value is 0x100 or 0x104, false, Modifiers(), timestamp);
            if (physical.Key == 0x1b && physical.Down) Passive.Observe(0x100, 0, 0, timestamp);
            var decision = ObserveWithFocus(physical);
            // Overflow fails closed: the key stays blocked, but no intent/action is issued.
            if (decision.Press is { } press && queue.TryEnqueue(press)) ready.Set();
            if (decision.Block) return new LRESULT(1);
        }
        return PInvoke.CallNextHookEx(default, code, message, data);
    }
    private unsafe LRESULT Mouse(int code, WPARAM message, LPARAM data)
    {
        if (code >= 0 && Passive.IsInterested)
        {
            var observed = *(MSLLHOOKSTRUCT*)data.Value;
            if (((uint)observed.flags & 3) == 0)
                Passive.Observe((uint)message.Value, observed.pt.X, observed.pt.Y,
                    clock.Milliseconds - unchecked((uint)clock.Milliseconds - observed.time));
        }
        if (code >= 0 && NativeHookEvent.IsMouseButton((uint)message.Value))
        {
            var mouse = *(MSLLHOOKSTRUCT*)data.Value;
            if (message.Value is 0x201 or 0x202)
                OverlayClicks.Observe(message.Value == 0x201, ((uint)mouse.flags & 3) != 0,
                    mouse.pt.X, mouse.pt.Y, clock.Milliseconds - unchecked((uint)clock.Milliseconds - mouse.time));
            if (!NativeHookEvent.MouseAllowed((uint)message.Value, (uint)mouse.flags))
                return PInvoke.CallNextHookEx(default, code, message, data);
            var now = clock.Milliseconds;
            var key = MouseButtonEvent.Decode((uint)message.Value, mouse.mouseData,
                false, KeyModifiers.None,
                now - unchecked((uint)now - mouse.time));
            if (key is { } physical)
            {
                var decision = ObserveWithFocus(physical with { Modifiers = Modifiers() });
                if (decision.Press is { } press && queue.TryEnqueue(press)) ready.Set();
                if (decision.Block) return new LRESULT(1);
            }
        }
        return PInvoke.CallNextHookEx(default, code, message, data);
    }
}
