using System.Text;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Logs;
using ExileSight.Features.Guide;
using ExileSight.Platform.Abstractions.Time;
using Xunit;

namespace ExileSight.Features.Guide.Tests;

public sealed class GuideRegressionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "guide-regression-" + Guid.NewGuid());
    private string ProgressPath => Path.Combine(directory, "progress.json");
    private sealed class Clock : IClock
    {
        public long Milliseconds { get; set; }
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch.AddMilliseconds(Milliseconds);
        public void Delay(int milliseconds) => Milliseconds += milliseconds;
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    [Theory]
    [InlineData(true, "oak", 5)]
    [InlineData(false, "none", 3)]
    public void NamesReplayAcrossActsWithDuplicateHintAndConditionalPages(bool league, string bandit, int count)
    {
        var data = GuideTests.Data();
        var pages = data.Pages.Take(5).ToArray();
        pages[1] = pages[1] with { Act = 1, Lines = [.. pages[1].Lines,
            new("(hint)__ areaidSyntheticLibrary", ["SyntheticLibrary"], [], [])] };
        pages[2] = pages[2] with { Condition = new("bandit", ["oak"]) };
        pages[3] = pages[3] with { Act = 2, Condition = new("league-start", ["yes"]) };
        data = data with { Pages = pages, Areas = [.. data.Areas, new("ActSixTown", "Lioneye's Watch", 6, 45, true, true, [])] };
        var p = new GuideProgress(data, league, bandit);
        var parser = new LogParser(); var visited = new List<int>();
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "guide-cross-act.synthetic.txt")))
        {
            var e = parser.Parse(line); Assert.IsType<AreaEntered>(e);
            p.Accept(GuideTests.Entry(e), e.Envelope!.UptimeMilliseconds); visited.Add(p.Index);
        }
        Assert.Equal(0, visited[0]); Assert.Equal(1, visited[1]); Assert.Equal(1, visited[2]);
        Assert.Equal(2, visited[3]); Assert.Equal(count - 1, p.Index); Assert.True(p.Complete);
    }
    [Fact]
    public void FallbackRetainsFilteringAndRecoversAcrossRestart()
    {
        var store = new GuideStore(directory);
        var id = store.WriteCustom(Encoding.UTF8.GetBytes("[[[\"areaidRemoved\"]]]"));
        store.Save(new(2, "old", 0, new long[11], false, false, false, "oak", null, id));
        var data = GuideTests.Data();
        data = data with { Pages = [data.Pages[0] with { Condition = new("league-start", ["yes"]) },
            data.Pages[1] with { Condition = new("bandit", ["oak"]) }] };
        var model = new GuideModel(store); model.Load(data, "new");
        var text = model.Snapshot().Text;
        Assert.Contains("League start: no · Bandit: oak", text);
        Assert.DoesNotContain("Lioneye's Watch", text); Assert.Contains("Southern Forest", text);
        Assert.Contains("incompatible", text); model.Checkpoint();
        var saved = new GuideStore(directory).Load()!;
        Assert.Equal("new", saved.Version); Assert.True(saved.CustomDisabled);
        var resumed = new GuideModel(new GuideStore(directory)); resumed.Load(data, "new");
        Assert.Contains("disabled", resumed.Snapshot().Text);
        resumed.RemoveCustom(); Assert.False(File.Exists(Path.Combine(directory, id + ".json")));
        Assert.Null(new GuideStore(directory).Load()!.CustomId);
    }
    [Fact]
    public void CheckpointsWriteSmallProgressOnlyWhenChangedAndNeverRewriteCustom()
    {
        var clock = new Clock(); var store = new GuideStore(directory);
        var model = new GuideModel(store, clock); model.Load(GuideTests.Data(), "v1");
        model.Checkpoint(); Assert.False(File.Exists(ProgressPath));
        model.Import(Encoding.UTF8.GetBytes("[[[\"Custom areaidSyntheticTown1\"]]]"));
        var state = new GuideStore(directory).Load()!;
        Assert.Null(state.Custom); Assert.NotNull(state.CustomId);
        var customPath = Path.Combine(directory, state.CustomId + ".json");
        var marker = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(ProgressPath, marker); File.SetLastWriteTimeUtc(customPath, marker);
        for (var i = 0; i < 180; i++) { clock.Milliseconds += 1000; model.Snapshot(); model.Checkpoint(); }
        Assert.Equal(marker, File.GetLastWriteTimeUtc(ProgressPath));
        model.Navigate(-1); // Same step, starts timing without changing persisted data.
        clock.Milliseconds += 60000; model.Checkpoint();
        Assert.Equal(60000, new GuideStore(directory).Load()!.Times.Sum());
        Assert.True(new FileInfo(ProgressPath).Length < 1024);
        Assert.Equal(marker, File.GetLastWriteTimeUtc(customPath));
        clock.Milliseconds += 60000; model.Checkpoint();
        Assert.Equal(120000, new GuideStore(directory).Load()!.Times.Sum());
        File.SetLastWriteTimeUtc(ProgressPath, marker);
        clock.Milliseconds += 600000; model.Checkpoint();
        Assert.Equal(marker, File.GetLastWriteTimeUtc(ProgressPath));
    }
    [Fact]
    public void HotkeysUseFunctionKeysWithoutChatEditingOrReservedDefaults()
    {
        var host = new ExileSight.Core.Features.FeatureHost([new GuideFeature()], (_, _) => null, (_, _, _) => { }, dispatch: work => work());
        Assert.Equal([0x7B, 0x7A, 0x79], host.DefaultBindings.Select(b => b.Key));
        Assert.All(host.Commands, command => Assert.Null(command.RequiredAction));
    }
}
