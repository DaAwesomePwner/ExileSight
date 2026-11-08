// Frozen 86cd546 baseline for cold-process latency comparison only.
using ExileSight.Core.Items;

namespace ExileSight.Features.ItemInfo.Tests;

public static class BaselineTextSection
{
    public static TooltipSection Render(ItemInfoResult result, string? status = null)
    {
        var rows = new List<TooltipRow>();
        void Add(string text, TooltipTone tone = TooltipTone.Normal) => rows.Add(new([new(text, tone)]));
        if (status is not null) Add(status, TooltipTone.Unknown);
        Add($"Prefixes: {result.Prefixes?.ToString() ?? "unknown"}   Suffixes: {result.Suffixes?.ToString() ?? "unknown"}");
        foreach (var mod in result.Mods)
        {
            var marker = mod.Highlight switch { Highlight.Desired => "[+] ", Highlight.Undesired => "[-] ", _ => "" };
            rows.Add(new([
                new(marker + mod.Source.Text + " "),
                new(mod.TierLabel + " ", Tone(mod.TierColor)),
                new(mod.RollPosition is { } roll ? $"{roll:0}% roll " : "roll unknown ", Tone(mod.RollColor)),
                new($"ilvl {mod.RequiredLevel?.ToString() ?? "unknown"}", mod.RequiredLevel is null ? TooltipTone.Unknown : TooltipTone.Normal)
            ]));
        }
        foreach (var defence in result.Defences)
            Add($"{defence.Name} base: {(defence.Percentile is { } percentile ? $"{percentile:0}%" : "unknown")}");
        if (result.Item.Rarity == ItemRarity.Unique) Add($"Unique drop tier: {result.UniqueDropTier?.ToString() ?? "unknown"}");
        return new("iteminfo", result.Item.BaseType ?? result.Item.Name, rows);
    }
    private static TooltipTone Tone(InfoColor color) => Enum.Parse<TooltipTone>(color.ToString());
}

// Retain only the old renderer's value types for the frozen latency baseline.
public enum TooltipTone { Normal, Unknown, Low, Medium, High, Best, Crafted, Unique }
public sealed record TooltipText(string Text, TooltipTone Tone = TooltipTone.Normal);
public sealed record TooltipRow(IReadOnlyList<TooltipText> Text);
public sealed record TooltipSection(string Id, string Title, IReadOnlyList<TooltipRow> Rows);
