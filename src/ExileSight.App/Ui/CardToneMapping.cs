using ExileSight.Features.ItemInfo;

namespace ExileSight.App.Ui;

public static class CardToneMapping
{
    public static CardTone ToCardTone(this InfoColor color) => color switch
    {
        InfoColor.Best => CardTone.Best,
        InfoColor.High => CardTone.High,
        InfoColor.Medium => CardTone.Medium,
        InfoColor.Low => CardTone.Low,
        InfoColor.Crafted => CardTone.Crafted,
        InfoColor.Unique => CardTone.Unique,
        _ => CardTone.Unknown
    };
}
