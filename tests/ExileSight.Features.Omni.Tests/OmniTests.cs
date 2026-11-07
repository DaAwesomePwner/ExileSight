using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using ExileSight.Features.Omni;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Shell;
using Xunit;

namespace ExileSight.Features.Omni.Tests;

public sealed class OmniTests
{
    private static string Fixture => Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "fixtures"),
        "*.plain.txt", SearchOption.AllDirectories).Select(File.ReadAllText).First();
    [Theory]
    [InlineData(0, false)]
    [InlineData(200, false)]
    [InlineData(201, true)]
    [InlineData(5000, true)]
    public void OneCopyAndOneDispatchPerPress(long duration, bool longPress)
    {
        var module = new OmniFeature();
        var copies = 0;
        var handler = new Handler(1, (_, _) => true);
        var host = new FeatureHost([module, new HandlerFeature(handler)], (_, _) => null, (_, _, _) => { },
            (_, _, action) => { Assert.IsType<GameAction.CopyItem>(action); copies++; return new(ActionOutcome.Success, CopiedText: Fixture); });
        var opened = 0;
        module.Menu.Opened += _ => opened++;
        var command = Assert.IsAssignableFrom<IPressCommandHandler>(Assert.Single(host.Commands));
        command.Press(new IntentToken(), new(1, 100));
        command.Press(new IntentToken(), new(1, 100)); // duplicate delivery is not another copy
        command.Release(new(2, 100, 100 + duration)); // unrelated release
        command.Release(new(1, 100, 100 + duration));
        command.Release(new(1, 100, 100 + duration));
        Assert.Equal(1, copies);
        Assert.Equal(longPress ? 1 : 0, opened);
        Assert.Equal(longPress ? 0 : 1, handler.Calls);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not item text")]
    public void EmptyOrInvalidCopyDoesNothing(string? text)
    {
        var module = new OmniFeature();
        var handler = new Handler(1, (_, _) => true);
        var host = new FeatureHost([module, new HandlerFeature(handler)], (_, _) => null, (_, _, _) => { },
            (_, _, _) => new(ActionOutcome.Success, CopiedText: text));
        var opened = 0;
        module.Menu.Opened += _ => opened++;
        var command = (IPressCommandHandler)host.Commands.Single();
        command.Press(new(), new(1, 100));
        command.Release(new(1, 100, 301));
        Assert.Equal(0, opened);
        Assert.Equal(0, handler.Calls);
    }
    [Fact]
    public void RoutesFixtureRaritiesAndClassesByPriorityThenRegistrationOrder()
    {
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "fixtures"), "*.plain.txt", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var item = ItemTextParser.Parse(File.ReadAllText(file), Game.PoE1).Item!;
            var context = new OmniContext(Game.PoE1, "test area");
            var never = new Handler(100, (_, _) => false);
            var first = new Handler(10, (i, c) => i.Rarity == item.Rarity && i.ItemClass == item.ItemClass && c == context);
            var second = new Handler(10, (_, _) => true);
            var low = new Handler(0, (_, _) => true);
            Assert.True(new OmniRouter([low, never, first, second]).Route(item, context));
            Assert.Equal(1, first.Calls);
            Assert.Equal(0, never.Calls + second.Calls + low.Calls);
            Assert.False(new OmniRouter([never]).Route(item, context));
        }
    }
    [Fact]
    public void ManifestAndDefaultBindingAreValid()
    {
        var module = new OmniFeature();
        module.Manifest.Validate();
        Assert.Equal(ActionKind.CopyItem, Assert.Single(module.Manifest.GameActions));
        Assert.Empty(module.Manifest.NetworkHosts);
        Assert.True(module.Manifest.BrowserHosts.SetEquals(OmniLinks.AllowedHosts));
        var host = new FeatureHost([module], (_, _) => null, (_, _, _) => { });
        var binding = Assert.Single(host.DefaultBindings);
        Assert.Equal(0x14, binding.Key);
        Assert.True(binding.Block);
        Assert.Equal("gameFocused", binding.Context);
        Assert.Equal("omni:press", binding.Command);
    }
    [Theory]
    [InlineData("Unique", "Foulborn A & B", "Gold Ring", "A_&_B")]
    [InlineData("Rare", "Unrelated Name", "Gold Ring", "Gold_Ring")]
    [InlineData("Normal", "Gold Ring", "Gold Ring", "Gold_Ring")]
    public void LinksEscapePagesAndNameTypeOnlyQuery(string rarity, string name, string type, string wiki)
    {
        var item = ItemTextParser.Parse($"Item Class: Rings\nRarity: {rarity}\n{name}\n{type}", Game.PoE1).Item!;
        var links = OmniLinks.Build(item, "League / &");
        foreach (var link in links) BrowserUrl.Validate(link.Url, OmniLinks.AllowedHosts);
        Assert.Equal("https://www.poewiki.net/wiki/" + Uri.EscapeDataString(wiki), links[0].Url);
        Assert.Equal("https://poedb.tw/us/Gold_Ring", links[1].Url);
        Assert.Contains("League%20%2F%20%26?q=", links[3].Url);
        using var json = JsonDocument.Parse(Uri.UnescapeDataString(links[3].Url.Split("?q=")[1]));
        var query = json.RootElement.GetProperty("query");
        Assert.Equal(type, query.GetProperty("type").GetString());
        Assert.Equal(rarity == "Unique" ? 2 : 1, query.EnumerateObject().Count());
        if (rarity == "Unique") Assert.Equal("A & B", query.GetProperty("name").GetString());
        Assert.Contains("/Standard?q=", OmniLinks.Build(item)[3].Url);
    }
    private sealed class Handler(int priority, Func<ParsedItem, OmniContext, bool> match) : IOmniHandler
    {
        public int Priority => priority;
        public int Calls { get; private set; }
        public bool Matches(ParsedItem item, OmniContext context) => match(item, context);
        public void Handle(ParsedItem item, OmniContext context, OmniPressKind press) { Assert.Equal(OmniPressKind.Short, press); Calls++; }
    }
    private sealed class HandlerFeature(IOmniHandler handler) : IFeature
    {
        public FeatureManifest Manifest { get; } = new("test", [Game.PoE1], [], [], [], "test");
        public void Register(IFeatureContext context) => context.RegisterOmniHandler(handler);
    }
}
