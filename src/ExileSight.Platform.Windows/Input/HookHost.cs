using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ExileSight.Platform.Windows.Input;

public sealed class HookHost : IHotkeys
{
    private readonly HotkeyDispatcher dispatcher;
    private readonly IClock clock;
    private readonly PressRouter router;
    private readonly Action<HotkeyBinding, ActionResult> report;
    private readonly PressQueue queue = new();
    private readonly EventWaitHandle stop = new(false, EventResetMode.ManualReset);
    private readonly AutoResetEvent ready = new(false);
    private readonly ManualResetEventSlim started = new();
    private readonly HOOKPROC keyboard;
    private readonly HOOKPROC mouse;
    private Thread? hookThread;
    private Thread? worker;
    private Thread? foregroundThread;
    private volatile Exception? startupError;
    private int disposed;
    private readonly HotkeyConfiguration configuration;
    public HookHost(HotkeyDispatcher dispatcher, IClock clock,
        Func<IntentToken?, HotkeyBinding, ActionResult> execute,
        Action<HotkeyBinding, ActionResult>? report = null)
    {
        configuration = dispatcher.Configuration;
        this.dispatcher = dispatcher; this.clock = clock;
        router = new(dispatcher, clock, GameForeground.IsGame, execute);
        this.report = report ?? ((_, _) => { });
        keyboard = Keyboard; mouse = Mouse;
    }
    public bool IsAlive => hookThread?.IsAlive == true && worker?.IsAlive == true && foregroundThread?.IsAlive == true && startupError is null;
    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        if (hookThread is not null) throw new InvalidOperationException("Hooks already started");
        worker = new Thread(Work) { IsBackground = true, Name = "Input actions" };
        hookThread = new Thread(Run) { IsBackground = true, Name = "Low-level input hooks" };
        hookThread.SetApartmentState(ApartmentState.STA);
        foregroundThread = new Thread(RefreshForeground) { IsBackground = true, Name = "Input foreground snapshot" };
        foregroundThread.Start(); worker.Start(); hookThread.Start(); started.Wait();
        if (startupError is not null) { Dispose(); throw new InvalidOperationException("Hook startup failed", startupError); }
    }
    private void Work()
    {
        try { Consume(); }
        catch (Exception error)
        {
            startupError = error;
            stop.Set();
        }
    }
    private unsafe void RefreshForeground()
    {
        try
        {
            // Dedicated observer: process discovery and its locks never run on the hook thread.
            // A short wait bounds refresh latency independently of slow action execution.
            nint previous = 0;
            do
            {
                var window = PInvoke.GetForegroundWindow();
                if ((nint)window.Value != previous)
                {
                    previous = (nint)window.Value;
                    configuration.UpdateGameFocused(false, previous);
                }
                var active = GameForeground.IsGame();
                if (window != PInvoke.GetForegroundWindow()) active = false;
                configuration.UpdateGameFocused(active, (nint)window.Value);
            } while (!stop.WaitOne(20));
        }
        catch (Exception error) { startupError = error; stop.Set(); ready.Set(); }
    }
    private void Consume()
    {
        while (!stop.WaitOne(0))
        {
            while (queue.TryDequeue(out var press))
            {
                if (stop.WaitOne(0)) return;
                report(press!.Binding, router.Route(press));
            }
            ready.WaitOne();
        }
    }
    private unsafe void Run()
    {
        HHOOK k = default, m = default;
        try
        {
            for (var key = 0; key < 256; key++)
                if (Down(key)) dispatcher.SeedHeld(key);
            var module = PInvoke.GetModuleHandle(null);
            k = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, keyboard, module, 0);
            m = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, mouse, module, 0);
            if (k.IsNull || m.IsNull) throw new InvalidOperationException("Low-level hook installation failed");
            started.Set();
            HANDLE handle = new(stop.SafeWaitHandle.DangerousGetHandle());
            while (!stop.WaitOne(0))
            {
                PInvoke.MsgWaitForMultipleObjectsEx(1, &handle, 100, QUEUE_STATUS_FLAGS.QS_ALLINPUT,
                    MSG_WAIT_FOR_MULTIPLE_OBJECTS_EX_FLAGS.MWMO_INPUTAVAILABLE);
                dispatcher.ReconcileHeld(Down);
                MSG message;
                while (PInvoke.PeekMessage(&message, default, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
                {
                    dispatcher.ReconcileHeld(Down);
                    PInvoke.TranslateMessage(&message);
                    PInvoke.DispatchMessage(&message);
                }
            }
        }
        catch (Exception error) { startupError = error; }
        finally
        {
            if (!k.IsNull) PInvoke.UnhookWindowsHookEx(k);
            if (!m.IsNull) PInvoke.UnhookWindowsHookEx(m);
            started.Set();
            stop.Set();
            ready.Set();
        }
    }
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
            var decision = dispatcher.Observe(physical, true);
            // Overflow fails closed: the key stays blocked, but no intent/action is issued.
            if (decision.Press is { } press && queue.TryEnqueue(press)) ready.Set();
            if (decision.Block) return new LRESULT(1);
        }
        return PInvoke.CallNextHookEx(default, code, message, data);
    }
    private unsafe LRESULT Mouse(int code, WPARAM message, LPARAM data)
    {
        if (code >= 0 && NativeHookEvent.IsMouseButton((uint)message.Value))
        {
            var mouse = *(MSLLHOOKSTRUCT*)data.Value;
            if (!NativeHookEvent.MouseAllowed((uint)message.Value, (uint)mouse.flags))
                return PInvoke.CallNextHookEx(default, code, message, data);
            var now = clock.Milliseconds;
            var key = MouseButtonEvent.Decode((uint)message.Value, mouse.mouseData,
                false, KeyModifiers.None,
                now - unchecked((uint)now - mouse.time));
            if (key is { } physical)
            {
                var decision = dispatcher.Observe(physical with { Modifiers = Modifiers() }, true);
                if (decision.Press is { } press && queue.TryEnqueue(press)) ready.Set();
                if (decision.Block) return new LRESULT(1);
            }
        }
        return PInvoke.CallNextHookEx(default, code, message, data);
    }
    public static KeyModifiers Modifiers()
    {
        var result = KeyModifiers.None;
        if (Down(0x11)) result |= KeyModifiers.Control;
        if (Down(0x12)) result |= KeyModifiers.Alt;
        if (Down(0x10)) result |= KeyModifiers.Shift;
        if (Down(0x5b) || Down(0x5c)) result |= KeyModifiers.Windows;
        return result;
    }
    private static bool Down(int key) => (PInvoke.GetAsyncKeyState(key) & 0x8000) != 0;
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stop.Set(); ready.Set(); hookThread?.Join(); worker?.Join(); foregroundThread?.Join();
        stop.Dispose(); ready.Dispose(); started.Dispose();
    }
}
