using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Windows.Input;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Input;

[CollectionDefinition("Desktop input", DisableParallelization = true)]
public sealed class DesktopInputCollection;

[Collection("Desktop input")]
public sealed class InputIntegrationTests
{
    [WindowsOnlyFact]
    public void ClipboardRestoresUnicodeAndEmpty()
    {
        var clipboard = new WindowsClipboard();
        var original = clipboard.Save();
        try
        {
            clipboard.SetText("ExileSight clipboard test — Ω");
            var snapshot = clipboard.Save();
            var sequence = clipboard.SequenceNumber;
            clipboard.SetText("Temporary text");
            Assert.NotEqual(sequence, clipboard.SequenceNumber);
            clipboard.Restore(snapshot);
            Assert.Equal("ExileSight clipboard test — Ω", clipboard.GetText());
            clipboard.Restore(null);
            Assert.Null(clipboard.GetText());
        }
        finally { clipboard.Restore(original); }
    }
    [WindowsOnlyFact]
    public void HooksInstallAndStopOnOwnedThread()
    {
        var clock = new SystemClock();
        using var hooks = new HookHost(new HotkeyDispatcher(new IntentRegistry(clock), []), clock,
            (_, _) => throw new InvalidOperationException("No binding should execute"));
        hooks.Start();
        Assert.True(hooks.IsAlive);
        hooks.Dispose();
        Assert.False(hooks.IsAlive);
    }
    [WindowsOnlyFact]
    public void GateRefusesOutsideGameAndConsumesTokenWithoutTouchingClipboard()
    {
        // Fail before issuing any intent if a game happens to be foreground on the test desktop.
        Assert.False(GameForeground.IsGame());
        var clock = new SystemClock();
        var registry = new IntentRegistry(clock);
        var dispatcher = new HotkeyDispatcher(registry, [new("test", 118, 0, true, new GameAction.CopyItem())]);
        var token = dispatcher.Issue(dispatcher.Observe(new(118, true, false, 0, clock.Milliseconds), true).Press!);
        var gate = new ActionGate(new GatePolicy(registry, [ActionKind.CopyItem]), new UntouchableClipboard(), clock);
        Assert.Equal(ActionOutcome.NotForeground, gate.TryExecute(token, new GameAction.CopyItem()).Outcome);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, gate.TryExecute(token, new GameAction.CopyItem()).Outcome);
        Assert.Equal(0, gate.ActionsSent);
    }
    private sealed class UntouchableClipboard : IClipboard
    {
        public uint SequenceNumber => throw new InvalidOperationException("Clipboard accessed");
        public string? GetText() => throw new InvalidOperationException("Clipboard accessed");
        public void SetText(string? text) => throw new InvalidOperationException("Clipboard accessed");
        public string? Save() => throw new InvalidOperationException("Clipboard accessed");
        public void Restore(string? saved) => throw new InvalidOperationException("Clipboard accessed");
    }
}
