using System.Collections.Concurrent;
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
    private readonly Action<IntentToken, HotkeyBinding> execute;
    private readonly ConcurrentQueue<PhysicalPress> queue = new();
    private readonly EventWaitHandle stop = new(false, EventResetMode.ManualReset);
    private readonly AutoResetEvent ready = new(false);
    private readonly ManualResetEventSlim started = new();
    private readonly HOOKPROC keyboard;
    private readonly HOOKPROC mouse;
    private Thread? hookThread;
    private Thread? worker;
    private Exception? startupError;
    private volatile bool active;
    private int disposed;
    public HookHost(HotkeyDispatcher dispatcher, IClock clock, Action<IntentToken, HotkeyBinding> execute)
    {
        this.dispatcher = dispatcher; this.clock = clock; this.execute = execute;
        keyboard = Keyboard; mouse = Mouse;
    }
    public bool IsAlive => hookThread?.IsAlive == true && startupError is null;
    public void Start()
    {
        if (hookThread is not null) throw new InvalidOperationException("Hooks already started");
        worker = new Thread(Work) { IsBackground = true, Name = "Input actions" };
        hookThread = new Thread(Run) { IsBackground = true, Name = "Low-level input hooks" };
        hookThread.SetApartmentState(ApartmentState.STA);
        worker.Start(); hookThread.Start(); started.Wait();
        if (startupError is not null) { Dispose(); throw new InvalidOperationException("Hook startup failed", startupError); }
    }
    private void Work()
    {
        while (!stop.WaitOne(0))
        {
            active = GameForeground.IsGame();
            while (queue.TryDequeue(out var press))
            {
                if (stop.WaitOne(0)) return;
                execute(dispatcher.Issue(press), press.Binding);
            }
            ready.WaitOne(20); // passive foreground cache refresh, never creates intent
        }
    }
    private unsafe void Run()
    {
        HHOOK k = default, m = default;
        try
        {
            k = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, keyboard, default, 0);
            m = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, mouse, default, 0);
            if (k.IsNull || m.IsNull) throw new InvalidOperationException("Low-level hook installation failed");
            started.Set();
            HANDLE handle = new(stop.SafeWaitHandle.DangerousGetHandle());
            while (!stop.WaitOne(0))
            {
                PInvoke.MsgWaitForMultipleObjectsEx(1, &handle, 100, QUEUE_STATUS_FLAGS.QS_ALLINPUT,
                    MSG_WAIT_FOR_MULTIPLE_OBJECTS_EX_FLAGS.MWMO_INPUTAVAILABLE);
                MSG message;
                while (PInvoke.PeekMessage(&message, default, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
                {
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
        }
    }
    private unsafe LRESULT Keyboard(int code, WPARAM message, LPARAM data)
    {
        if (code >= 0)
        {
            var key = *(KBDLLHOOKSTRUCT*)data.Value;
            var injected = (key.flags & KBDLLHOOKSTRUCT_FLAGS.LLKHF_INJECTED) != 0;
            // Hook timestamps share the system tick epoch; unsigned subtraction handles 32-bit wrap.
            var now = clock.Milliseconds;
            var timestamp = now - unchecked((uint)now - key.time);
            var decision = dispatcher.Observe(new((int)key.vkCode,
                message.Value is 0x100 or 0x104, injected, Modifiers(), timestamp), active);
            if (decision.Press is { } press) { queue.Enqueue(press); ready.Set(); }
            if (decision.Block) return new LRESULT(1);
        }
        return PInvoke.CallNextHookEx(default, code, message, data);
    }
    private static LRESULT Mouse(int code, WPARAM message, LPARAM data) =>
        PInvoke.CallNextHookEx(default, code, message, data); // no mouse token issuance in Phase 0
    public static KeyModifiers Modifiers()
    {
        var result = KeyModifiers.None;
        if (Down(0x11)) result |= KeyModifiers.Control;
        if (Down(0x12)) result |= KeyModifiers.Alt;
        if (Down(0x10)) result |= KeyModifiers.Shift;
        if (Down(0x5b) || Down(0x5c)) result |= KeyModifiers.Windows;
        return result;
    }
    private static bool Down(int key) => (PInvoke.GetAsyncKeyState((VIRTUAL_KEY)key) & 0x8000) != 0;
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stop.Set(); ready.Set(); hookThread?.Join(); worker?.Join();
        stop.Dispose(); ready.Dispose(); started.Dispose();
    }
}
