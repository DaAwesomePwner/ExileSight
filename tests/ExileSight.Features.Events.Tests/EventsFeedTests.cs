using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ExileSight.Core.Features;
using ExileSight.Core.Logs;
using ExileSight.Core.Settings;
using ExileSight.Core.Setup;
using ExileSight.Features.Events;
using ExileSight.Platform.Abstractions.Files;
using Xunit;

namespace ExileSight.Features.Events.Tests;

public sealed class EventsFeedTests
{
    [Fact]
    public async Task SyntheticLinesTravelThroughParserFeedAndFeatureSubscriptionWithoutDuplicateToasts()
    {
        var root = Path.Combine(Path.GetTempPath(), "events-feed-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            using var settings = new JsonSettingsStore<SettingsDocument>(new AppPaths(root), Path.Combine(root, "settings.json"),
                SettingsJsonContext.Default.SettingsDocument, () => new());
            settings.Update("general", s => s.General.ClientTxtPath = Path.Combine(root, "Client.txt"));
            var tail = new Tail();
            await using var session = new ClientLogSession(settings, tail);
            var feature = new EventsFeature();
            var received = Channel.CreateUnbounded<ClientLogEvent>();
            var host = new FeatureHost([new ObservedFeature(feature, value => received.Writer.TryWrite(value))],
                (_, _) => null, (_, _, _) => { });
            feature.Model.Configure(EventsModelTests.Definitions, _ => true);
            using var logs = host.ConnectLogs(session.Events);
            async Task<ClientLogEvent> Next() => await received.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await session.RefreshAsync();
            Assert.IsType<LogSourceChanged>((await Next()).Event);
            var lines = File.ReadLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "events-synthetic.txt")).Skip(1).ToArray();
            long previous = 1;
            // Replaying identical live lines, including the same generated instance, must not enqueue duplicates.
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var line in lines)
                {
                    Assert.True(tail.Lines.Writer.TryWrite(line));
                    var delivered = await Next();
                    Assert.Equal(++previous, delivered.Offset);
                    Assert.False(delivered.IsReplay);
                }
                Assert.Equal("MercenaryEncounter", feature.Model.Current?.Id);
                Assert.Equal(3, feature.Model.PendingCount);
            }
            await logs.StopAsync(TimeSpan.FromSeconds(5));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class ObservedFeature(EventsFeature feature, Action<ClientLogEvent> observe) : IFeature
    {
        public FeatureManifest Manifest => feature.Manifest;
        public void Register(IFeatureContext context)
        {
            feature.Register(context);
            context.SubscribeLog(observe); // Same owner queue: observation follows the model's callback.
        }
    }

    private sealed class Tail : IFileTail
    {
        public Channel<string> Lines { get; } = Channel.CreateUnbounded<string>();
        public async IAsyncEnumerable<string> ReadLinesAsync(string path, long? position = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var line in Lines.Reader.ReadAllAsync(cancellationToken)) yield return line;
        }
    }
}
