using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ExileSight.Core.Input;
using ExileSight.Platform.Windows.Input;

namespace ExileSight.App.Features;

/// <summary>One UI drain and shared visibility lease for every passive overlay.</summary>
public sealed class PassiveOverlayInput : IDisposable
{
    private readonly HookHost hooks;
    private readonly Dictionary<object, (long OpenedAt, Action<PassiveInput> Observe)> observers = [];
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(16) };
    public PassiveOverlayInput(HookHost hooks, ILogger<PassiveOverlayInput>? logger = null)
    {
        this.hooks = hooks;
        if (logger is not null) hooks.Passive.ReportFailure = type => logger.LogWarning("Passive observer failed: {ExceptionType}", type);
        timer.Tick += Drain;
        hooks.Passive.Observed += Observe;
    }
    public void Open(object owner, Action<PassiveInput> observe)
    {
        if (observers.Count == 0) hooks.Passive.Open(hooks.InputTimestamp);
        observers[owner] = (hooks.InputTimestamp, observe);
        timer.Start();
    }
    public void Close(object owner)
    {
        observers.Remove(owner);
        if (observers.Count != 0) return;
        timer.Stop();
        hooks.Passive.Close();
    }
    private void Drain(object? sender, EventArgs args) => hooks.Passive.Drain();
    private void Observe(PassiveInput input)
    {
        foreach (var observer in observers.Values.ToArray())
            if (input.Timestamp >= observer.OpenedAt)
            {
                try { observer.Observe(input); }
                catch (Exception error)
                {
                    try { hooks.Passive.ReportFailure(error.GetType().Name); }
                    catch (Exception) { /* Continue delivering dismissal to other overlays. */ }
                }
            }
    }
    public void Dispose()
    {
        void Stop()
        {
            timer.Stop();
            timer.Tick -= Drain;
            hooks.Passive.Observed -= Observe;
            hooks.Passive.Close();
            observers.Clear();
        }
        if (timer.Dispatcher.CheckAccess()) Stop(); else timer.Dispatcher.Invoke(Stop);
    }
}
