using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Pricing;
using ExileSight.Features.Stashninja;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class EngineTests
{
    private static Layout Fixture() => Layouts.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "synthetic.json")));
    [Fact] public void LayoutRequiresProvenanceAndValidCells()
    {
        var l = Fixture();
        Assert.Equal(new Point(310, 320), Layouts.Origin(l, l.Cells[1]));
        Assert.Throws<JsonException>(() => Layouts.Validate(l with { Provenance = l.Provenance with { Method = "copied" } }));
        Assert.Throws<JsonException>(() => Layouts.Validate(l with { Cells = [l.Cells[0], l.Cells[0]] }));
        Assert.Throws<JsonException>(() => Layouts.Validate(l with { Cells = [l.Cells[0], l.Cells[1] with { X = double.NaN }] }));
        Assert.Throws<JsonException>(() => Layouts.Validate(l with { Cells = [l.Cells[0], l.Cells[1] with { Folder = "missing" }] }));
        Assert.Throws<JsonException>(() => Layouts.Validate(l with { Cells = [l.Cells[0], l.Cells[1] with { W = 99999 }] }));
        Assert.Throws<JsonException>(() => Layouts.Parse("null"));
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "synthetic.json"));
        Assert.Throws<JsonException>(() => Layouts.Parse(json.Replace("\"x\": 100,", "")));
        Assert.Throws<JsonException>(() => Layouts.Parse(json.Replace("\"tabId\"", "\"unknown\"")));
    }
    [Fact] public void GeometryScalesAndCompensatesBars()
    {
        Assert.Equal(new Transform(2, 0, 0), Geometry.Fit(new(1920, 1080), new(3840, 2160)));
        Assert.Equal(new Transform(1, 460, 0), Geometry.Fit(new(1920, 1080), new(3440, 1080)));
        Assert.Equal(new Transform(1, -460, 0), Geometry.Fit(new(3440, 1080), new(1920, 1080)));
        var t = Geometry.Calibrate(new(10, 10), new(50, 50), new(25, 30), new(105, 110));
        Assert.Equal(new Transform(2, 5, 10), t);
        Assert.Throws<ArgumentException>(() => Geometry.Calibrate(new(0, 0), new(0, 0), new(0, 0), new(1, 1)));
        Assert.Throws<ArgumentException>(() => Geometry.Calibrate(new(0, 0), new(100, 100), new(0, 0), new(100, 20)));
    }
    [Fact] public void PricesConvertColorAndRetainSparklineGaps()
    {
        var overview = new PriceOverview([new("chaos", "Chaos Orb", "Currency", null, 1, null, [10, null, -10], null, null),
            new("divine", "Divine Orb", "Currency", null, 200, null, [], null, null)], 200);
        var tags = Tags.Build(Fixture(), overview, new("test", 10, 100, "#FFFFFF", "#FFD700"));
        Assert.Equal("1d", tags[1].Text);
        Assert.Equal("#FFD700", tags[1].Color);
        Assert.Equal(7, tags[0].Sparkline.Length);
        Assert.Null(tags[0].Sparkline[5]);
        Assert.Equal(99, tags[0].Sparkline[6]!.Value, 6);
        Assert.Contains("Divine", Tags.Search(tags, 10).Text);
        Assert.Null(Tags.Search(tags, 201).Text);
        Assert.Equal("1", Tags.Build(Fixture(), overview with { ChaosPerDivine = null }, new("t",0,1,"#FFFFFF","#FFFFFF"))[0].Text);
    }
    [Fact] public void RegexEscapesNamesAndEnforcesExactLimit()
    {
        var c = Fixture().Cells[0];
        PriceTag Tag(string name) => new(c, name, 10, "10c", "#FFFFFF", []);
        Assert.Equal(250, Tags.Search([Tag(new string('a',248))], 1).Text!.Length);
        Assert.Null(Tags.Search([Tag(new string('a',249))], 1).Text);
        Assert.Equal("\"A\\(B\\)\"", Tags.Search([Tag("A(B)")], 1).Text);
        Assert.Null(Tags.Search([Tag("bad\"name")], 1).Text);
        Assert.Null(Tags.Search([Tag("bad\nname")], 1).Text);
    }
    [Fact] public void ManifestAndCommandRequireSeparateAuthority()
    {
        int calls = 0;
        var model = new StashModel();
        var host = new FeatureHost([new StashninjaFeature(model)], (_, _) => null, (_, _, _) => { },
            (_, _, action) => { calls++; Assert.IsType<GameAction.PasteSearch>(action); return new(ActionOutcome.Success); });
        Assert.Equal([ActionKind.PasteSearch], host.Manifests.Single().GameActions);
        var command = host.Commands.Single(c => c.Command == "stashninja:highlight");
        Assert.Equal(CommandKind.OverlayOnly, Assert.IsAssignableFrom<IFeatureCommandPolicy>(command).Kind);
        model.SetSearch(new("\"Chaos Orb\"", "ready"));
        Assert.Equal(ActionOutcome.MissingToken, command.Execute().Outcome);
        Assert.Equal(0, calls);
        Assert.Equal(ActionOutcome.Success, command.Execute(new IntentToken()).Outcome);
        Assert.Equal(1, calls);
        model.SetSearch(new(new string('a', 251), "too long"));
        Assert.Equal(ActionOutcome.Failed, command.Execute(new IntentToken()).Outcome);
        Assert.Equal(1, calls);
    }
    [Fact] public void EmptyAndDuplicateLayoutDirectoriesAreHandled()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Empty(Layouts.LoadDirectory(directory));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "layout.schema.json"), "{}");
            Assert.Empty(Layouts.LoadDirectory(directory));
            var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "synthetic.json"));
            File.WriteAllText(Path.Combine(directory, "a.layout.json"), json);
            Assert.Single(Layouts.LoadDirectory(directory));
            File.WriteAllText(Path.Combine(directory, "b.layout.json"), json);
            Assert.Throws<JsonException>(() => Layouts.LoadDirectory(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact] public void CalibrationRoundTripsAndPreservesCorruptFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var l = Fixture(); var r = new Resolution(1920,1080); var t = new Transform(1,2,3);
            new CalibrationStore(directory).Save(l,r,t);
            var store = new CalibrationStore(directory);
            Assert.Equal(t, store.Get(l,r));
            Assert.Null(store.Get(l,new(2560,1440)));
            Assert.Null(store.Get(l with { Page = "2" },r));
            File.WriteAllText(Path.Combine(directory,"calibration.json"), "broken");
            store = new CalibrationStore(directory);
            Assert.NotNull(store.Notice);
            Assert.Single(Directory.GetFiles(directory,"*.corrupt"));
            store.Save(l,r,t);
            Assert.Equal(t,new CalibrationStore(directory).Get(l,r));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
    }
}
