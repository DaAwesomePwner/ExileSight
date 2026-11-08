using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class DefenceReviewTests
{
    public static TheoryData<string, string, string, string> Kinds => new()
    {
        { "Armour", "armour", "local_base_physical_damage_reduction_rating", "local_physical_damage_reduction_rating_+%" },
        { "Evasion Rating", "evasion", "local_base_evasion_rating", "local_evasion_rating_+%" },
        { "Energy Shield", "energy_shield", "local_energy_shield", "local_energy_shield_+%" },
        { "Ward", "ward", "local_ward", "local_ward_+%" }
    };
    private static PackContent Pack(BaseEntry[] bases, StatTranslation[] translations) =>
        new("synthetic", Game.PoE1, 3, "test", bases, [], Translations: translations);
    private static StatTranslation Translation(string id, string text) =>
        new(id, [id], [new(text, [new()], ["#"], [[]])]);

    [Theory]
    [MemberData(nameof(Kinds))]
    public void RecoversExactPercentilesAndRejectsImpossibleDisplays(string property, string key, string flat, string inc)
    {
        var cases = JsonSerializer.Deserialize<Case[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures/defence-cases.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach (var c in cases)
        {
            var item = ItemTextParser.Parse($"Item Class: Helmets\nRarity: Rare\nSynthetic\nSynthetic Hat\n--------\nQuality: +{c.Quality}%\n{property}: {c.Shown}\n--------\n+{c.Flat} to Test\n{c.Increased}% increased Test", Game.PoE1).Item!;
            var basis = new BaseEntry("b", "Synthetic Hat", "Helmet", ["helmet"], 1, [], [],
                new() { [key] = new(c.Min, c.Max) });
            var data = new PackItemInfoData(Pack([basis], []), null,
                Pack([], [Translation(flat, "+{0} to Test"), Translation(inc, "{0}% increased Test")]));
            Assert.Equal(c.Percentile, Assert.Single(new ItemInfoCalculator(data).Calculate(item).Defences).Percentile);
        }
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task LocalGlobalCollisionsAndUnrelatedRoundedStatsDoNotHidePercentile(
        string property, string key, string flat, string inc)
    {
        // Optional manual evidence run substitutes generated pinned packs; normal CI uses synthetic translations.
        var directory = Environment.GetEnvironmentVariable("EXILESIGHT_ITEMINFO_REVIEW_PACKS");
        var basis = new BaseEntry("b", "Synthetic Hat", "Helmet", ["helmet"], 1, [], [],
            new() { [key] = new(100, 180) });
        var translations = new[]
        {
            Translation(flat, "+{0} to " + property), Translation(inc, "{0}% increased " + property),
            Translation("global_" + key, "+{0} to " + property),
            Translation("global_" + key + "_increase", "{0}% increased " + property),
            new StatTranslation("rounded", ["unrelated"], [new("{0} Test per second",
                [new()], ["#"], [["per_minute_to_per_second"]])])
        };
        PackContent bases = Pack([basis], []), stats = Pack([], translations);
        var flatText = "+10 to " + property;
        var incText = "20% increased " + property;
        if (directory is not null)
        {
            bases = JsonSerializer.Deserialize(File.ReadAllBytes(Path.Combine(directory, "poe1-bases.json")), PackJson.Default.PackContent)!;
            stats = JsonSerializer.Deserialize(File.ReadAllBytes(Path.Combine(directory, "poe1-stats.json")), PackJson.Default.PackContent)!;
            Assert.Equal("d0bc438357d849bb93ad22f3db0b12938aaf6da1", stats.PackVersion);
            basis = bases.Bases.First(b => b.ItemClass == "Helmet" && b.Defences.TryGetValue(key, out var r) && r.Max > r.Min);
            flatText = StatText.Render(stats.Translations!.First(t => t.StatIds.SequenceEqual([flat])), [10])!;
            incText = StatText.Render(stats.Translations!.First(t => t.StatIds.SequenceEqual([inc])), [20])!;
            // Preserve an unrelated rounded line alongside real-pack collisions.
            stats = stats with { Translations = [.. stats.Translations!, translations[^1]] };
        }
        var range = basis.Defences[key];
        var rolled = (range.Min + range.Max) / 2;
        var shown = decimal.Round((rolled + 10) * 1.4m, 0, MidpointRounding.AwayFromZero);
        var item = ItemTextParser.Parse($"Item Class: Helmets\nRarity: Rare\nSynthetic\n{basis.Name}\n--------\nQuality: +20%\n{property}: {shown}\n--------\n{flatText}\n{incText}\n33.3 Test per second", Game.PoE1).Item!;
        var data = new PackItemInfoData(bases, null, stats);
        await data.PrepareAsync();
        Assert.True(data.MatchLine(item.Mods[0]).Length >= (key == "ward" && directory is not null ? 1 : 2));
        Assert.Contains(data.MatchLine(item.Mods[^1]), m => m.Values.Any(v => v.Min != v.Max));
        Assert.Equal((rolled - range.Min) * 100m / (range.Max - range.Min),
            Assert.Single(new ItemInfoCalculator(data).Calculate(item).Defences).Percentile);
    }

    public sealed record Case(int Min, int Max, int Flat, int Increased, int Quality, int Shown, decimal? Percentile);
}
