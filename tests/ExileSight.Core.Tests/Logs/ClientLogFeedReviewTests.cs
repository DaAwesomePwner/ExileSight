using System.Threading.Channels;
using ExileSight.Core.Features;
using ExileSight.Core.Logs;
using Xunit;

namespace ExileSight.Core.Tests.Logs;

public sealed partial class ClientLogFeedTests
{
    [Fact]
    public async Task FeatureCallbacksAreSerialAndRegistrationOrderedButOtherOwnersProgress()
    {
        await using var rig = new Rig();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var calls = Channel.CreateUnbounded<string>();
        var other = Channel.CreateUnbounded<ClientLogEvent>();
        var host = new FeatureHost([
            new Module(c =>
            {
                c.SubscribeLog(e => { if (e.Event is AreaEntered) calls.Writer.TryWrite("envelope"); });
                c.Subscribe<AreaEntered>(_ =>
                {
                    calls.Writer.TryWrite("area"); entered.Set(); release.Wait(TimeSpan.FromSeconds(10));
                    throw new InvalidOperationException("secret");
                });
                c.Subscribe<AreaEntered>(_ => calls.Writer.TryWrite("second-area"));
                c.Subscribe<CharacterLevelUp>(_ => calls.Writer.TryWrite("level"));
            }),
            new Module(c => c.SubscribeLog(e => other.Writer.TryWrite(e)), "other")
        ], (_, _) => null, (_, _, _) => { });
        using var connections = host.ConnectLogs(rig.Session.Events);
        try
        {
            await rig.Session.RefreshAsync(); rig.Send(0);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            rig.Tail.Lines.Writer.TryWrite(": Someone (Witch) is now level 23");
            await Next(other); Assert.IsType<CharacterLevelUp>((await Next(other)).Event);
            Assert.Equal("envelope", await Next(calls)); Assert.Equal("area", await Next(calls));
            Assert.False(calls.Reader.TryRead(out _));
            Assert.Contains("feedtest:", rig.Session.Events.Diagnostic);
            Assert.Contains("other:", rig.Session.Events.Diagnostic);
        }
        finally { release.Set(); }
        Assert.Equal("second-area", await Next(calls)); Assert.Equal("level", await Next(calls));
        await connections.StopAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LargeReplayAndLiveBurstHaveNoSeamLoss()
    {
        await using var rig = new Rig();
        var live = Channel.CreateUnbounded<ClientLogEvent>();
        using var witness = rig.Session.Events.Subscribe(e => live.Writer.TryWrite(e));
        await rig.Session.RefreshAsync();
        for (var i = 0; i < 1024; i++) { rig.Send(i % 60); await Next(live); }
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var delivered = Channel.CreateUnbounded<ClientLogEvent>();
        using var subscription = rig.Session.Events.Subscribe(e =>
        {
            if (e.Offset == 2) { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); }
            delivered.Writer.TryWrite(e);
        }, new(MaxEvents: 1024), capacity: 2);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            rig.Send(1); rig.Send(2); await Next(live); await Next(live);
        }
        finally { release.Set(); }
        for (var i = 0; i < 1026; i++)
        {
            var value = await Next(delivered);
            Assert.Equal(i + 2, value.Offset); Assert.Equal(i < 1024, value.IsReplay);
            Assert.Equal(0, value.DroppedBefore);
        }
        Assert.Equal(0, subscription.DroppedEvents);
    }

    [Fact]
    public async Task ReplayRequestsArePerHandlerAndPreserveRegistrationOrder()
    {
        await using var rig = new Rig();
        var live = Channel.CreateUnbounded<ClientLogEvent>();
        using var witness = rig.Session.Events.Subscribe(e => live.Writer.TryWrite(e));
        await rig.Session.RefreshAsync();
        for (var i = 0; i < 4; i++) { rig.Send(i); await Next(live); }
        var calls = Channel.CreateUnbounded<string>();
        var host = new FeatureHost([new Module(c =>
        {
            c.SubscribeLog(e => calls.Writer.TryWrite($"a{e.Offset}"), new(MaxEvents: 2));
            c.SubscribeLog(e => calls.Writer.TryWrite($"b{e.Offset}"), new(MaxEvents: 1));
            c.Subscribe<AreaEntered>(_ => calls.Writer.TryWrite("live"));
        })], (_, _) => null, (_, _, _) => { });
        using var connection = host.ConnectLogs(rig.Session.Events);
        Assert.Equal("a4", await Next(calls)); Assert.Equal("a5", await Next(calls));
        Assert.Equal("b5", await Next(calls));
        rig.Send(5);
        Assert.Equal("a6", await Next(calls)); Assert.Equal("b6", await Next(calls));
        Assert.Equal("live", await Next(calls));
    }

    [Fact]
    public async Task StopWaitsForRunningCallbackAndHonorsDeadline()
    {
        await using var rig = new Rig();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var host = new FeatureHost([new Module(c => c.Subscribe<AreaEntered>(_ =>
        { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); }))], (_, _) => null, (_, _, _) => { });
        using var connection = host.ConnectLogs(rig.Session.Events);
        try
        {
            await rig.Session.RefreshAsync(); rig.Send(0);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            await Assert.ThrowsAsync<TimeoutException>(() => connection.StopAsync(TimeSpan.FromMilliseconds(50)));
            var stopping = connection.StopAsync(TimeSpan.FromSeconds(5));
            Assert.False(stopping.IsCompleted);
            release.Set(); await stopping;
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task PathChangeIsDeliveredBeforeNewSourceDataEvenWhenNewFileIsSilent()
    {
        await using var rig = new Rig();
        var received = Channel.CreateUnbounded<ClientLogEvent>();
        using var subscription = rig.Session.Events.Subscribe(e => received.Writer.TryWrite(e));
        await rig.Session.RefreshAsync();
        var first = await received.Reader.ReadAsync(); Assert.IsType<LogSourceChanged>(first.Event);
        rig.Settings.Update("general", s => s.General.ClientTxtPath = Path.Combine(rig.Root, "Other.txt"));
        await rig.Session.RefreshAsync();
        var changed = await received.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<LogSourceChanged>(changed.Event); Assert.True(changed.Generation > first.Generation);
        rig.Send(1); Assert.Equal(changed.Generation, (await Next(received)).Generation);
    }
}
