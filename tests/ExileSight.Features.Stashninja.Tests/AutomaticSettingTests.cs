using System.Text.Json;
using ExileSight.Core.Features;
using Xunit;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class AutomaticSettingTests
{
    [Fact]
    public void DefaultsOnAndPersistsOffAcrossRegistration()
    {
        var values = new Dictionary<string, JsonElement>();
        StashModel Register()
        {
            var model = new StashModel();
            _ = new FeatureHost([new StashninjaFeature(model)],
                (_, key) => values.TryGetValue(key, out var value) ? value : null,
                (owner, key, value) => { Assert.Equal("stashninja", owner); values[key] = value.Clone(); });
            return model;
        }
        var first = Register();
        Assert.True(first.AutomaticPrices);
        first.SetAutomaticPrices(false);
        Assert.False(Register().AutomaticPrices);
        first.SetAutomaticPrices(true);
        Assert.True(Register().AutomaticPrices);
    }
}
