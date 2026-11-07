using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ExileSight.Platform.Windows.Input;

public sealed partial class HookHost : IHotkeys
{
    private readonly HotkeyDispatcher dispatcher;
    private readonly IClock clock;
    private readonly KeyboardRepeatSettings repeatSettings;
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
    private Timer? repeatTimer;
    private readonly HookForeground foreground;
    private volatile Exception? startupError;
    private int disposed;
    private readonly HotkeyConfiguration configuration;
    internal HookHost(HotkeyDispatcher dispatcher, IClock clock,
        Func<IntentToken?, HotkeyBinding, ActionResult> execute, KeyboardRepeatSettings repeatSettings,
        Action<HotkeyBinding, ActionResult>? report = null, IGameWindow? gameWindow = null)
    {
        this.repeatSettings = repeatSettings;
        ArgumentNullException.ThrowIfNull(gameWindow);
        foreground = new(gameWindow, dispatcher.Configuration, clock);
        dispatcher.SetRepeatProvider(() => repeatSettings.SilenceMilliseconds);
        configuration = dispatcher.Configuration;
        this.dispatcher = dispatcher; this.clock = clock;
        router = new(dispatcher, clock, GameForeground.IsGame, execute,
            (token, press) => FindPressCommand(press.Binding) is { } command
                ? command.Press(token, new(press.Id, press.Timestamp)) : execute(token, press.Binding),
            (binding, released) => FindPressCommand(binding)?.Release(released));
        this.report = report ?? ((_, _) => { });
        keyboard = Keyboard; mouse = Mouse;
    }
    public bool IsAlive => hookThread?.IsAlive == true && worker?.IsAlive == true && startupError is null;
    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        if (hookThread is not null) throw new InvalidOperationException("Hooks already started");
        repeatSettings.Refresh(); // caller thread, before the hook can receive any input
        worker = new Thread(Work) { IsBackground = true, Name = "Input actions" };
        worker.SetApartmentState(ApartmentState.MTA);
        hookThread = new Thread(Run) { IsBackground = true, Name = "Low-level input hooks" };
        hookThread.SetApartmentState(ApartmentState.STA);
        foreground.Start();
        repeatTimer = new Timer(_ => RefreshRepeat(), null, 1000, 1000);
        worker.Start(); hookThread.Start(); started.Wait();
        if (startupError is not null) { Dispose(); throw new InvalidOperationException("Hook startup failed", startupError); }
    }
    private void Work()
    {
        try
        {
            Volatile.Write(ref workerApartment, (int)Thread.CurrentThread.GetApartmentState());
            Consume();
        }
        catch (Exception error)
        {
            startupError = error;
            stop.Set();
        }
    }
    private void RefreshRepeat()
    {
        try { repeatSettings.Refresh(); }
        catch (Exception error) { startupError = error; stop.Set(); ready.Set(); }
    }
    private void Consume()
    {
        while (!stop.WaitOne(0))
        {
            DrainPassive();
            ReportIgnoredHotkey();
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
        stop.Set(); ready.Set(); hookThread?.Join(); worker?.Join();
        foreground.Dispose();
        if (repeatTimer is not null)
        {
            using var drained = new ManualResetEvent(false);
            if (repeatTimer.Dispose(drained)) drained.WaitOne();
        }
        stop.Dispose(); ready.Dispose(); started.Dispose();
    }
}
