using System.Security;
using ExileSight.Platform.Abstractions.Install;
using Microsoft.Win32;

namespace ExileSight.Platform.Windows.Install;

/// <summary>Read-only install hints. Never examines a game process or configuration file.</summary>
public sealed class InstallLocations : IInstallLocations
{
    public IEnumerable<string> StandaloneLocations() => RegistryValues(@"Software\GrindingGear Games\Path of Exile", "InstallLocation");
    public IEnumerable<string> SteamLocations() => RegistryValues(@"Software\Valve\Steam", "SteamPath")
        .Concat(RegistryValues(@"Software\Valve\Steam", "InstallPath"));

    private static IEnumerable<string> RegistryValues(string path, string name)
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            string? value = null;
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(path, writable: false);
                value = key?.GetValue(name) as string;
            }
            catch (Exception error) when (error is SecurityException or UnauthorizedAccessException or IOException) { }
            if (!string.IsNullOrWhiteSpace(value)) yield return value;
        }
    }

    public IEnumerable<string> DefaultLocations()
    {
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.Distinct())
        {
            yield return Path.Combine(root, "Grinding Gear Games", "Path of Exile");
            yield return Path.Combine(root, "Steam", "steamapps", "common", "Path of Exile");
            yield return Path.Combine(root, "Epic Games", "PathOfExile");
            yield return Path.Combine(root, "Epic Games", "Path of Exile");
        }
    }

    public string? ReadLibraryFolders(string steamRoot)
    {
        try { return File.ReadAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf")); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }
}
