using System.Reflection;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;
using ExileSight.Platform.Abstractions.Window;
using ExileSight.Platform.Windows.Input;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Input;

public sealed class OverlayRedemptionTests
{
    [Theory]
    [InlineData("unknown")]
    [InlineData("owner:local")]
    public void UnknownOrLocalCommandCannotMintAndConsumesClick(string command)
    {
        using var hooks = Create();
        Click(hooks);
        Assert.Null(hooks.RedeemOverlayClick(123, 20, 20, command));
        Assert.Null(hooks.RedeemOverlayClick(123, 20, 20, "owner:copy"));
    }
    [Fact]
    public void RedeemedTokenIsStampedWithRegisteredOwnerAndAction()
    {
        using var hooks = Create();
        Click(hooks);
        var token = hooks.RedeemOverlayClick(123, 20, 20, "owner:copy");
        Assert.NotNull(token);
        // Inspect the existing private registry without creating an alternate issuer or sending input.
        var issuer = typeof(HookHost).GetField("overlayIssuer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hooks)!;
        var registry = (IntentRegistry)issuer.GetType().GetProperty("Registry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(issuer)!;
        Assert.Equal(ActionOutcome.Success, registry.Consume(token, out var press));
        Assert.Equal("owner", press!.FeatureId);
        Assert.Equal("owner:copy", press.HotkeyId);
        Assert.Equal(ActionKind.CopyItem, press.Action);
        Assert.Equal(100, press.Timestamp);
        Assert.Null(hooks.RedeemOverlayClick(123, 20, 20, "owner:copy"));
    }
    private static void Click(HookHost hooks)
    {
        hooks.OverlayClicks.SetTargets([new(123, 0, 0, 100, 100)]);
        hooks.OverlayClicks.Observe(true, false, 20, 20, 50);
        hooks.OverlayClicks.Observe(false, false, 20, 20, 100);
    }
    private static HookHost Create() => HookHost.Create(new Clock(), new(), new UntouchedClipboard(), new GameWindow(),
        (_, _) => throw new InvalidOperationException(), (_, _) => { },
        [new FeatureManifest("owner", [Game.PoE1], [ActionKind.CopyItem], [], [], "Test")],
        [new Command("owner:copy", ActionKind.CopyItem), new Command("owner:local", null)]);
    private sealed class Command(string name, ActionKind? action) : IFeatureCommandHandler
    {
        public string FeatureId => "owner";
        string IFeatureCommandHandler.Command => name;
        public ActionKind? RequiredAction => action;
        public ActionResult Execute() => throw new InvalidOperationException();
    }
    private sealed class Clock : IClock
    {
        public long Milliseconds => 100;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public void Delay(int milliseconds) => throw new InvalidOperationException();
    }
    private sealed class GameWindow : IGameWindow
    {
        public GameWindowState Current => GameWindowState.Missing;
        public string? LastError => null;
        public event EventHandler? Changed { add { } remove { } }
        public void Start() => throw new InvalidOperationException();
        public void Dispose() { }
    }
    private sealed class UntouchedClipboard : IClipboard
    {
        public bool PasteConsumed => throw new InvalidOperationException();
        public uint SequenceNumber => throw new InvalidOperationException();
        public void OfferText(string text) => throw new InvalidOperationException();
        public void Pump() => throw new InvalidOperationException();
        public string? GetText() => throw new InvalidOperationException();
        public void SetText(string? text) => throw new InvalidOperationException();
        public ClipboardSnapshot Save() => throw new InvalidOperationException();
        public void Restore(ClipboardSnapshot? saved) => throw new InvalidOperationException();
    }
}
