using ExileSight.Core.Features;
using ExileSight.Core.Logs;
using ExileSight.Features.MapTracker;
using ExileSight.Platform.Abstractions.Input;
using Xunit;
namespace ExileSight.Features.MapTracker.Tests;

public sealed class FeatureTests
{
    [Fact]
    public void KillsCommandSendsExactlyOnceForEachGateAcceptedToken()
    {
        var consumed = new HashSet<IntentToken>(); var sent = new List<GameAction>();
        var model = new TrackerModel(new RunStore(Path.GetTempPath()));
        var host = new FeatureHost([new MapTrackerFeature(model)], (_, _) => null, (_, _, _) => { },
            (_, token, action) =>
            {
                // Gate test double enforces the same single-use token contract as GatePolicy.
                if (token is null) return new(ActionOutcome.MissingToken);
                if (!consumed.Add(token)) return new(ActionOutcome.UsedOrUnknownToken);
                sent.Add(action); return new(ActionOutcome.Success);
            });
        var command = Assert.Single(host.Commands, c => c.Command == "maptracker:kills");
        var token = new IntentToken();
        Assert.Equal(ActionOutcome.Success, command.Execute(token).Outcome);
        Assert.Equal(ActionOutcome.UsedOrUnknownToken, command.Execute(token).Outcome);
        Assert.Equal(new GameAction.SendChatCommand("/kills"), Assert.Single(sent));
        Assert.Equal(ActionOutcome.Success, command.Execute(new IntentToken()).Outcome);
        Assert.Equal(2, sent.Count);
    }
    [Fact]
    public void PassiveEventsAndTokenlessCommandsCannotSendKills()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var model = new TrackerModel(new RunStore(directory)); var sends = 0;
            var host = new FeatureHost([new MapTrackerFeature(model)], (_, _) => null, (_, _, _) => { },
                (_, _, _) => { sends++; return new(ActionOutcome.Success); });
            model.SelectLeague("Synthetic league");
            model.Accept(new(1, 1, new LogSourceChanged(), false));
            var command = Assert.Single(host.Commands, c => c.Command == "maptracker:kills");
            Assert.Equal(ActionOutcome.MissingToken, command.Execute().Outcome); Assert.Equal(0, sends);
            Assert.Equal(ActionKind.SendChatCommand, command.RequiredAction);
            Assert.Equal(2, host.DefaultBindings.Count);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
