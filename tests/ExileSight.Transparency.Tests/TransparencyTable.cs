using System.Globalization;
using ExileSight.Core.Features;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Transparency.Tests;

internal static class TransparencyTable
{
    internal const string Start = "<!-- transparency:start -->", End = "<!-- transparency:end -->";
    internal const string UpdateCommand = "UPDATE_TRANSPARENCY=1 dotnet test tests/ExileSight.Transparency.Tests";

    internal static string Generate(FeatureHost host, string root)
    {
        var ids = host.Manifests.Select(m => m.Id).Order(StringComparer.Ordinal).ToArray();
        if (!ids.SequenceEqual(DeclaredResources.FeatureWrites.Keys.Order(StringComparer.Ordinal)))
            throw new InvalidOperationException("Declare file writes for every current feature in DeclaredResources.FeatureWrites.");
        var lines = new List<string>
        {
            Start, "## Transparency", "",
            "Generated from compiled feature manifests and registered default bindings; supplemental file writes and App services are declared in `tests/ExileSight.Transparency.Tests/DeclaredResources.cs`.", "",
            "| Feature | Game actions | Data packs | Hosts | Writes | Default keys |",
            "|---|---|---|---|---|---|"
        };
        foreach (var manifest in host.Manifests.OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            var hosts = manifest.NetworkHosts.Select(h => "HTTP: " + h)
                .Concat(manifest.BrowserHosts.Select(h => "Browser: " + h));
            lines.Add(Row(manifest.Id, Cell(manifest.GameActions.Select(a => a.ToString())), Cell(manifest.DataPacks),
                Cell(hosts), Cell(DeclaredResources.FeatureWrites[manifest.Id]),
                Cell(host.DefaultBindings.Where(b => b.Command.StartsWith(manifest.Id + ":", StringComparison.Ordinal)).Select(Key))));
        }
        lines.AddRange(["", "Omni owns the initial CopyItem action for other item tools. Browser hosts open in your default browser; they are separate from app HTTP access.", "",
            "Paths are relative to `%APPDATA%/ExileSight` (or the configured portable root), except user-selected exports and Velopack install files. Packs and the Action Gate audit always use `%APPDATA%/ExileSight`. Active profile JSON is `poe1/{profile-id}.json`. Stores may also create temporary, backup, recovery and corrupt-preservation files (`.tmp`, `.bak`, `.recovery`, `.corrupt*`).", "",
            "### App services", "", "| Service | HTTP hosts | Writes |", "|---|---|---|"]);
        foreach (var service in DeclaredResources.Services(root))
            lines.Add(Row(service.Name, Cell(service.Hosts), Cell(service.Writes)));
        lines.AddRange(["", "Regenerate from the repository root (PowerShell: set `$env:UPDATE_TRANSPARENCY='1'` first):", "",
            "```sh", UpdateCommand, "```", End]);
        return string.Join('\n', lines);
    }

    private static string Cell(IEnumerable<string> values)
    {
        var entries = values.Order(StringComparer.Ordinal).Select(v => "`" + v.Replace("`", "&#96;")
            .Replace("|", "&#124;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\n", " ").Replace("\r", " ") + "`");
        var result = string.Join("<br>", entries);
        return result.Length == 0 ? "None" : result;
    }
    private static string Row(params string[] cells) => "| " + string.Join(" | ", cells) + " |";
    private static string Key(FeatureDefaultBinding binding)
    {
        var key = binding.Key switch
        {
            0x09 => "Tab", 0x14 => "CapsLock",
            >= 0x70 and <= 0x87 => "F" + (binding.Key - 0x6F).ToString(CultureInfo.InvariantCulture),
            >= 0x30 and <= 0x5A => ((char)binding.Key).ToString(),
            _ => "VK 0x" + binding.Key.ToString("X2", CultureInfo.InvariantCulture)
        };
        var modifiers = binding.Modifiers == KeyModifiers.None ? "" : binding.Modifiers.ToString().Replace(", ", "+") + "+";
        return modifiers + key + " → " + binding.Command + (binding.Block ? " (blocked)" : " (passed through)");
    }
}
