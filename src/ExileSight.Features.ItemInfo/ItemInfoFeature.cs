using System.ComponentModel;
using ExileSight.Core.Features;
using ExileSight.Core.Games;
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo;

public sealed class ItemInfoViewModel : INotifyPropertyChanged
{
    public ItemInfoResult? Current { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Show(ItemInfoResult result)
    {
        Current = result;
        PropertyChanged?.Invoke(this, new(nameof(Current)));
    }
    public void Close()
    {
        Current = null;
        PropertyChanged?.Invoke(this, new(nameof(Current)));
    }
}

[DeclaredActions]
public sealed class ItemInfoFeature : IFeature
{
    public ItemInfoFeature() { }
    public ItemInfoFeature(IItemInfoData data) => Data = data;
    public IItemInfoData? Data { get; }
    public ItemInfoViewModel ViewModel { get; } = new();
    public FeatureManifest Manifest { get; } = new("iteminfo", [Game.PoE1], [],
        ["poe1/bases", "poe1/mods", "poe1/stats"], [], "Copied item tiers, rolls and highlights");
    public void Register(IFeatureContext context)
    {
        context.RegisterOverlay(ViewModel);
        context.RegisterOmniHandler(new ItemInfoHandler(new(Data), ViewModel, () => ItemInfoSettings.Load(context)));
    }
}

public sealed class ItemInfoHandler(ItemInfoCalculator calculator, ItemInfoViewModel viewModel,
    Func<ItemInfoSettings> settings) : IOmniHandler
{
    private static readonly HashSet<string> Classes = new(StringComparer.Ordinal)
    {
        "Helmets", "Body Armours", "Gloves", "Boots", "Shields", "Quivers", "Belts", "Amulets", "Rings",
        "One Hand Swords", "Thrusting One Hand Swords", "Two Hand Swords", "One Hand Axes", "Two Hand Axes",
        "One Hand Maces", "Two Hand Maces", "Sceptres", "Daggers", "Rune Daggers", "Claws", "Wands",
        "Staves", "Warstaves", "Bows", "Jewels", "Abyss Jewels", "Cluster Jewels",
        "Life Flasks", "Mana Flasks", "Hybrid Flasks", "Utility Flasks"
    };
    public int Priority => 100;
    public static bool Supports(ParsedItem item) => item.Game == Game.PoE1 && Classes.Contains(item.ItemClass) &&
        item.Rarity is ItemRarity.Normal or ItemRarity.Magic or ItemRarity.Rare or ItemRarity.Unique;
    public bool Matches(ParsedItem item, OmniContext context) => context.Game == Game.PoE1 && Supports(item);
    public void Handle(ParsedItem item, OmniContext context, OmniPressKind press)
    {
        if (Matches(item, context)) viewModel.Show(calculator.Calculate(item, settings()));
    }
}
