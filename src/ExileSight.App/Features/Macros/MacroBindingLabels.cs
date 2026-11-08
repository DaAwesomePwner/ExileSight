using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.App.Features.Macros;

public static class MacroBindingLabels
{
    public static string Title(IEnumerable<HotkeyBinding> bindings)
    {
        var snapshot = bindings.ToArray();
        string Keys(string command)
        {
            var labels = snapshot.Where(b => b.FeatureId == "macros" && b.FeatureCommand == command)
                .Select(b => (b.Modifiers == KeyModifiers.None ? "" : b.Modifiers.ToString().Replace(", ", " + ") + " + ") + Key(b.Key));
            return string.Join(", ", labels.DefaultIfEmpty("Unbound"));
        }
        return $"Macros — {Keys("macros:travel")} travel / {Keys("macros:macro")} commands";
    }
    private static string Key(int key) => key switch
    {
        >= 0x70 and <= 0x87 => "F" + (key - 0x6f),
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5a => ((char)key).ToString(),
        0x20 => "Space", 0x14 => "CapsLock", 0x09 => "Tab",
        _ => $"VK {key:X2}"
    };
}
