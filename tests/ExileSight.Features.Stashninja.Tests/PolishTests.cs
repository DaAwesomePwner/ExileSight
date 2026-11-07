using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Features.Stashninja;
using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class PolishTests
{
    [Theory]
    [InlineData("Orb of Alteration", "Orb of Alteration")]
    [InlineData("Blacksmith's Whetstone", "Blacksmith's Whetstone")]
    [InlineData("Test #1 (a+b).[x]", @"Test #1 \(a\+b\)\.\[x\]")]
    public void SearchPreservesOrdinaryCharacters(string name, string expected)
    {
        var cell = new Cell("test", 1, 1, 10, 10, "Currency", ItemName: name);
        var result = Tags.Search([new(cell, name, 1, "1c", "#FFFFFF", [])], 0);
        Assert.Equal("\"" + expected + "\"", result.Text);
        Assert.Equal(expected.Length + 2, result.Text!.Length);
    }
    [Fact] public void LimitCountsEscapesAndQuotes()
    {
        PriceTag Tag(string name) => new(new("t",0,0,1,1,"Currency"),name,1,"1c","#FFFFFF",[]);
        Assert.Equal(250, Tags.Search([Tag(new string('(',124))],0).Text!.Length);
        Assert.Null(Tags.Search([Tag(new string('(',125))],0).Text);
        Assert.Equal(250, Tags.Search([Tag(new string(' ',247) + "#")],0).Text!.Length);
    }
    [Theory]
    [InlineData(1920,1080,0.5)]
    [InlineData(1920,1080,2.0)]
    [InlineData(3440,1440,1.25)]
    public void ViewportExactlyCoversClientAtAnyScale(int width, int height, double scale)
    {
        var size = Geometry.Viewport(new(width,height),scale);
        Assert.Equal(width, size.X * scale, 8);
        Assert.Equal(height, size.Y * scale, 8);
        Assert.Throws<ArgumentException>(() => Geometry.Viewport(new(width,height),0));
    }
    [Theory]
    [InlineData(100,200,250,100)]
    [InlineData(600,200,100,200)]
    [InlineData(200,600,200,100)]
    public void CalibrationAcceptsArbitraryOrderedPairs(double ax, double ay, double bx, double by)
    {
        var expected = new Transform(1.5,12,-30);
        var a = new Point(ax,ay); var b = new Point(bx,by);
        var result = Geometry.Calibrate(a,b,expected.Apply(a),expected.Apply(b));
        Assert.Equal(expected,result);
    }
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    [InlineData("42")]
    public void InvalidProfilesProduceVisibleNoticeAndKeepDefaults(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var model = new StashModel();
        _ = new FeatureHost([new StashninjaFeature(model)], (_,_) => doc.RootElement, (_,_,_) => { });
        Assert.NotNull(model.ProfileNotice);
        Assert.Equal(2,model.Profiles.Length);
    }
    [Fact] public void CalibrationKeysAreBoundedHashes()
    {
        var layout = Layouts.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","synthetic.json")));
        var path = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CalibrationStore(path);
            store.Save(layout,new(1920,1080),new(1,2,3));
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(path,"calibration.json")));
            Assert.Equal(64,document.RootElement.EnumerateObject().Single().Name.Length);
            Assert.Equal(new Transform(1,2,3),store.Get(layout,new(1920,1080)));
            Assert.Null(store.Get(layout with { Cells = [layout.Cells[0] with { X = 150 },layout.Cells[1]] },new(1920,1080)));
        }
        finally { Directory.Delete(path,true); }
    }
}
