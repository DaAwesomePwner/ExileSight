using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Features.Events;
using Xunit;

namespace ExileSight.Features.Events.Tests;

public sealed class EventsFeatureTests
{
    [Fact]
    public void FeatureLoadsPackDefaultsPersistsChoicesAndKeepsOverridesOnRefresh()
    {
        var publisher = new PackPublisher();
        Publish(publisher, "v1");
        var settings = new Dictionary<string, JsonElement> { ["enabled.Hideout"] = JsonSerializer.SerializeToElement(true) };
        var feature = new EventsFeature();
        var host = new FeatureHost([feature], (_, key) => settings.TryGetValue(key, out var value) ? value : null,
            (owner, key, value) => { Assert.Equal("events", owner); settings[key] = value.Clone(); },
            dispatch: work => work(), packs: publisher.Reader);
        Assert.Empty(feature.Manifest.GameActions);
        Assert.Equal("poe1/events", Assert.Single(feature.Manifest.DataPacks));
        Assert.True(host.HasLogSubscriptions);
        Assert.Same(feature.Model, Assert.Single(host.Overlays).ViewModel);
        Assert.True(feature.Model.Options.Single(o => o.Event.Id == "Hideout").Enabled);
        feature.Model.SetEnabled("Seer", false);
        Assert.False(settings["enabled.Seer"].GetBoolean());
        Publish(publisher, "v2");
        Assert.False(feature.Model.Options.Single(o => o.Event.Id == "Seer").Enabled);
        Assert.Equal("Updated Seer", feature.Model.Options.Single(o => o.Event.Id == "Seer").Event.DisplayName);
        var requested = false;
        feature.Model.SettingsRequested += () => requested = true;
        Assert.Null(Assert.Single(host.Commands).RequiredAction);
        Assert.Single(host.Commands).Execute();
        Assert.True(requested);
        publisher.Publish("poe1/events", null, "none");
        Assert.Empty(feature.Model.Options);
    }

    [Fact]
    public void SettingsHaveALocalDefaultBinding()
    {
        var host = new FeatureHost([new EventsFeature()], (_, _) => null, (_, _, _) => { });
        Assert.Equal("events:settings", Assert.Single(host.DefaultBindings).Command);
    }

    [Fact]
    public void MissingPackAndInvalidSettingDoNotInventDisplayTextOrEnableHideouts()
    {
        var feature = new EventsFeature();
        var publisher = new PackPublisher();
        _ = new FeatureHost([feature], (_, _) => JsonSerializer.SerializeToElement("invalid"), (_, _, _) => { },
            dispatch: work => work(), packs: publisher.Reader);
        Assert.Empty(feature.Model.Options);
        Publish(publisher, "v1");
        Assert.False(feature.Model.Options.Single(o => o.Event.Id == "Hideout").Enabled);
        Assert.True(feature.Model.Options.Single(o => o.Event.Id == "Seer").Enabled);
    }

    private static void Publish(PackPublisher publisher, string version)
    {
        var entries = EventsModelTests.Definitions.Select(d => version == "v2" && d.Id == "Seer"
            ? d with { DisplayName = "Updated Seer" } : d).ToArray();
        var content = new PackContent("poe1/events", Game.PoE1, 3, version, [], [], Events: new(entries));
        publisher.Publish("poe1/events", new(new("poe1/events", Game.PoE1, 3, version, "unknown", DateTimeOffset.UnixEpoch,
            new("synthetic", "synthetic", []), "Synthetic ExileSight test definitions", PackCodec.Hash(PackCodec.ContentBytes(content))),
            content), "fixture");
    }
}
