using System.Text.RegularExpressions;
using ExileSight.Platform.Abstractions.Install;

namespace ExileSight.Core.Setup;

public static class ClientLogDiscovery
{
    // Names/order from Exile-UI, Exile UI.ahk first-launch log selection (~1190–1210).
    public static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[] { "Client.txt", "KakaoClient.txt", "LatestClient.txt" });

    public static IEnumerable<string> SteamLibraries(string vdf)
    {
        foreach (Match match in Regex.Matches(vdf, "\"(?:path|[0-9]+)\"\\s*\"([^\"]+)\"",
                     RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            var value = match.Groups[1].Value.Replace(@"\\", @"\");
            if (IsLocalAbsolutePath(value)) yield return value;
        }
    }

    public static IReadOnlyList<string> Candidates(IInstallLocations locations, ILocalPathResolver? resolver = null)
    {
        var roots = new List<string>(locations.StandaloneLocations());
        foreach (var steam in locations.SteamLocations().Where(p => IsLocalPath(p, resolver)))
        {
            roots.Add(Path.Combine(steam, "steamapps", "common", "Path of Exile"));
            foreach (var library in SteamLibraries(locations.ReadLibraryFolders(steam) ?? ""))
                roots.Add(Path.Combine(library, "steamapps", "common", "Path of Exile"));
        }
        roots.AddRange(locations.DefaultLocations());
        return roots.Where(p => IsLocalPath(p, resolver))
            .SelectMany(root => Names.Select(name => Path.Combine(root, "logs", name)))
            .Where(p => IsLocalPath(p, resolver)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool IsLocalAbsolutePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("//", StringComparison.Ordinal)) return false;
        // Recognize Windows drive paths in portable discovery tests as well.
        return normalized.Length >= 3 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':' && normalized[2] == '/'
            || !OperatingSystem.IsWindows() && Path.IsPathFullyQualified(path);
    }

    public static bool IsLocalPath(string? path, ILocalPathResolver? resolver = null)
    {
        if (!IsLocalAbsolutePath(path)) return false;
        if (resolver is null) return true;
        try
        {
            if (resolver.IsRemoteDrive(path!)) return false;
            var final = resolver.FinalPath(path!);
            if (final is null) return false;
            if (final.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return false;
            if (final.StartsWith(@"\\?\", StringComparison.Ordinal)) final = final[4..];
            return IsLocalAbsolutePath(final) && !resolver.IsRemoteDrive(final);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { return false; }
    }

    public static string? Validate(string? path, ILocalPathResolver? resolver = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Choose a Client.txt log file.";
        if (!IsLocalPath(path, resolver)) return "Choose a fully-qualified local path; network paths are not allowed.";
        if (!Names.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            return "Choose Client.txt, KakaoClient.txt or LatestClient.txt.";
        try
        {
            if (!File.Exists(path)) return "The selected log file does not exist.";
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return "The selected log cannot be opened for reading."; }
    }
}
