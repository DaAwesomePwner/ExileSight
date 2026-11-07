using System.Text;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Logs;
using ExileSight.Features.Guide;
using Xunit;

namespace ExileSight.Features.Guide.Tests;

public sealed class GuideTests
{
    internal static GuideData Data()
    {
        GuideAreaData[] areas = [new("SyntheticTown1", "Lioneye's Watch", 1, 1, true, true, []),
            new("SyntheticForest", "The Southern Forest", 2, 12, false, true, []),
            new("SyntheticTown2", "The Forest Encampment", 2, 13, true, true, []),
            new("SyntheticCity", "The City of Sarn", 3, 22, false, true, []),
            new("SyntheticTown3", "The Sarn Encampment", 3, 23, true, true, []),
            new("SyntheticLibrary", "The Library", 3, 30, false, true, [])];
        return new(areas.Select((a, i) => new GuidePageData(a.Act, i + 1, null,
            [new("Enter areaid" + a.Id + " (lvl:3)", [a.Id], [], ["3"])])).ToArray(), areas, []);
    }
    internal static ClientLogEvent Entry(LogEvent e) => new(1, 0, e, false);
    private static void Enter(GuideProgress p, string name, long now) => p.Accept(Entry(new AreaEntered("", null, name)), now);
    [Fact]
    public void SyntheticActsOneThroughThreeReplay()
    {
        var p = new GuideProgress(Data());
        var parser = new LogParser();
        var steps = new List<int>();
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "guide-acts1-3.synthetic.txt")))
        {
            var e = parser.Parse(line);
            Assert.IsNotType<Unknown>(e);
            p.Accept(Entry(e), e.Envelope!.UptimeMilliseconds);
            if (e is AreaEntered) steps.Add(p.Index);
        }
        Assert.Equal([0, 1, 2, 3, 4, 5, 5], steps);
        Assert.True(p.Complete);
        Assert.Equal(11000, p.ActMilliseconds[1]);
        Assert.Equal(19000, p.ActMilliseconds[2]);
        Assert.Equal(30000, p.ActMilliseconds[3]);
    }
    [Fact]
    public void GraceAndManualBackClamp()
    {
        var p = new GuideProgress(Data()); p.Navigate(-1, 1000);
        Enter(p, "Lioneye's Watch", 2999); Assert.Equal(0, p.Index);
        Enter(p, "Lioneye's Watch", 3000); Assert.Equal(1, p.Index);
        p.Navigate(-1, 4000); Assert.Equal(0, p.Index);
    }
    [Fact]
    public void ExactIdWinsAndIsConsumedOnce()
    {
        var p = new GuideProgress(Data());
        p.Accept(Entry(new AreaGenerated("", null, 1, "SyntheticForest", 1)), 0);
        Enter(p, "Lioneye's Watch", 100); Assert.Equal(0, p.Index);
        Enter(p, "Lioneye's Watch", 200); Assert.Equal(1, p.Index);
    }
    [Fact]
    public void AmbiguousNamesUseActContextAndHintsDoNotAdvance()
    {
        var data = Data();
        var first = data.Pages[0] with { Lines = [.. data.Pages[0].Lines, new("(hint)__ areaidSyntheticForest", ["SyntheticForest"], [], [])] };
        data = data with { Pages = [first, .. data.Pages.Skip(1)], Areas = [.. data.Areas, new("OtherTown", "Lioneye's Watch", 6, 1, true, true, [])] };
        var p = new GuideProgress(data); Enter(p, "Lioneye's Watch", 0); Assert.Equal(1, p.Index);
    }
    [Fact]
    public void HideoutAndGapsKeepStepAndFlagUncertainty()
    {
        var p = new GuideProgress(Data()); Enter(p, "Hideout", 0); Assert.Equal(0, p.Index);
        p.Accept(Entry(new AreaEntered("", null, "Lioneye's Watch")) with { DroppedBefore = 3 }, 100);
        Assert.Equal(0, p.Index); Assert.True(p.Uncertain);
        p.Accept(Entry(new LogSourceChanged()), 200); Assert.Equal(0, p.Index);
        p.Tick(10000); Assert.Equal(0, p.ActMilliseconds.Sum());
    }
    [Fact]
    public void TimersPauseAtIdleAndAfkAndExportInvariantCsv()
    {
        var p = new GuideProgress(Data()); Enter(p, "Other", 0); p.Tick(300000);
        Assert.Equal(120000, p.ActMilliseconds[1]);
        p.Accept(Entry(new CharacterLevelUp("", null, "NeverPersist", "Witch", 9)), 400000);
        p.Accept(Entry(new AfkModeChanged("", null, true)), 405000); p.Tick(500000);
        Assert.Equal(125000, p.ActMilliseconds[1]); Assert.Equal(9, p.Level);
        Assert.Contains("total,125.000,false\r\n", p.Csv());
    }
    [Fact]
    public void ImportConditionsAndDisplay()
    {
        var data = GuideImport.Read(Encoding.UTF8.GetBytes("""[[["areaidSyntheticTown1"],{"condition":["bandit","oak"],"lines":["areaidSyntheticTown1"]}]]"""), Data());
        Assert.Null(new GuideProgress(data).Next);
        Assert.NotNull(new GuideProgress(data, bandit: "oak").Next);
        Assert.Equal("Lioneye's Watch (lvl:3)", GuideDisplay.Line("areaidSyntheticTown1 (lvl:3)", data));
    }
    [Theory]
    [InlineData("[]")]
    [InlineData("[[[\"areaidUnknown\"]]]")]
    [InlineData("[[[\"<unknown_quest> areaidSyntheticTown1\"]]]")]
    [InlineData("[[{\"condition\":[\"execute\",\"yes\"],\"lines\":[\"areaidSyntheticTown1\"]}]]")]
    [InlineData("[[[42]]]")]
    public void ImportRejectsInvalidSchemaAndReferences(string json) =>
        Assert.Throws<ArgumentException>(() => GuideImport.Read(Encoding.UTF8.GetBytes(json), Data()));
    [Fact]
    public void ImportIsBounded() => Assert.Throws<ArgumentException>(() => GuideImport.Read(new byte[GuideImport.MaxBytes + 1], Data()));
}
