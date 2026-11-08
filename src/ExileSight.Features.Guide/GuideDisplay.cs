// Display markup reference: Exile-UI modules/leveling tracker.ahk (MIT).
using System.Text.RegularExpressions;
using ExileSight.Core.DataPacks;

namespace ExileSight.Features.Guide;

public static class GuideDisplay
{
    public static string Line(string text, GuideData data)
    {
        text = Regex.Replace(text, @"areaid([A-Za-z0-9_]+)", m =>
            data.Areas.FirstOrDefault(a => a.Id == m.Groups[1].Value)?.Name ?? m.Value,
            RegexOptions.None, TimeSpan.FromMilliseconds(100));
        text = Regex.Replace(text, @"\((?:img|color):[^)]+\)", "", RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
        return text.Replace("(hint)__", "Hint: ").Replace("_", " ").Replace("<", "").Replace(">", "");
    }
}
