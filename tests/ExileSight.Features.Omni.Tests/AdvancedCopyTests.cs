using System.Text.Json;
using ExileSight.Core.Features;
using ExileSight.Core.Items;
using ExileSight.Features.Omni;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Features.Omni.Tests;

public sealed class AdvancedCopyTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("{}", true)]
    [InlineData("null", true)]
    [InlineData("[]", true)]
    [InlineData("false", true)]
    [InlineData("{\"UseAdvancedCopy\":\"bad\"}", true)]
    [InlineData("{\"UseAdvancedCopy\":null}", true)]
    [InlineData("{\"UseAdvancedCopy\":false}", false)]
    [InlineData("{\"UseAdvancedCopy\":true}", true)]
    public void EachPressCopiesOnceWithConfiguredOrDefaultFlag(string? json, bool advanced)
    {
        var actions = new List<GameAction>();
        var host = new FeatureHost([new OmniFeature()], (_, key) =>
            key == "settings" && json is not null ? JsonSerializer.Deserialize<JsonElement>(json) : null,
            (_, _, _) => { }, (_, _, action) => { actions.Add(action); return new(ActionOutcome.Success); });
        var command = Assert.IsAssignableFrom<IPressCommandHandler>(Assert.Single(host.Commands));
        var token = new IntentToken();
        command.Press(token, new(1, 100));
        command.Press(token, new(1, 100));
        command.Release(new(1, 100, 200));
        Assert.Equal(advanced, Assert.IsType<GameAction.CopyItem>(Assert.Single(actions)).Advanced);
    }

    [Fact]
    public void SavedSettingsTakeEffectOnTheNextPressWithoutRestarting()
    {
        var feature = new CapturingFeature();
        var settings = new Dictionary<string, JsonElement>();
        var flags = new List<bool>();
        var host = new FeatureHost([feature], (_, key) => settings.TryGetValue(key, out var value) ? value : null,
            (owner, key, value) => { Assert.Equal("omni", owner); settings[key] = value; },
            (_, _, action) => { flags.Add(Assert.IsType<GameAction.CopyItem>(action).Advanced); return new(ActionOutcome.Success); });
        var command = Assert.IsAssignableFrom<IPressCommandHandler>(Assert.Single(host.Commands));
        command.Press(new(), new(1, 100));
        new OmniSettings(UseAdvancedCopy: false).Save(feature.Context);
        command.Press(new(), new(2, 200));
        new OmniSettings().Save(feature.Context);
        command.Press(new(), new(3, 300));
        Assert.Equal(new[] { true, false, true }, flags);
    }

    public static IEnumerable<object[]> AdvancedFixtures() => Directory.GetFiles(
        Path.Combine(AppContext.BaseDirectory, "fixtures"), "*.advanced.txt", SearchOption.AllDirectories)
        .Select(path => new object[] { Path.GetRelativePath(Path.Combine(AppContext.BaseDirectory, "fixtures"), path) });

    [Theory]
    [MemberData(nameof(AdvancedFixtures))]
    public void AdvancedFixturesReachTheOmniHandler(string fixture)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", fixture));
        var receiver = new CapturingFeature();
        var actions = new List<GameAction>();
        var host = new FeatureHost([receiver], (_, _) => null, (_, _, _) => { },
            (_, _, action) => { actions.Add(action); return new(ActionOutcome.Success, CopiedText: text); },
            dispatch: work => work());
        var command = Assert.IsAssignableFrom<IPressCommandHandler>(Assert.Single(host.Commands));
        command.Press(new(), new(1, 100));
        command.Release(new(1, 100, 200));
        Assert.True(Assert.IsType<GameAction.CopyItem>(Assert.Single(actions)).Advanced);
        var item = Assert.Single(receiver.Items);
        Assert.False(string.IsNullOrWhiteSpace(item.ItemClass));
        if (fixture.EndsWith("two-affix-magic.advanced.txt", StringComparison.Ordinal))
        {
            Assert.Equal("Gold Ring", item.BaseType);
            Assert.Collection(item.Mods,
                mod => { Assert.Equal(6, mod.Tier); Assert.Equal(20m, Assert.Single(mod.Values).Minimum); Assert.Equal(29m, Assert.Single(mod.Values).Maximum); },
                mod => { Assert.Equal(4, mod.Tier); Assert.Equal(10m, Assert.Single(mod.Values).Minimum); Assert.Equal(15m, Assert.Single(mod.Values).Maximum); });
        }
    }

    private sealed class CapturingFeature : IFeature, IOmniHandler
    {
        private readonly OmniFeature omni = new();
        public FeatureManifest Manifest => omni.Manifest;
        public IFeatureContext Context { get; private set; } = null!;
        public List<ParsedItem> Items { get; } = [];
        public int Priority => 0;
        public void Register(IFeatureContext context)
        {
            Context = context;
            omni.Register(context);
            context.RegisterOmniHandler(this);
        }
        public bool Matches(ParsedItem item, OmniContext context) => true;
        public void Handle(ParsedItem item, OmniContext context, OmniPressKind press) => Items.Add(item);
    }
}
