using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.Core.Settings;

/// <summary>Path lookup never creates directories. An explicit root enables portable mode.</summary>
public sealed class AppPaths : IAppPaths
{
    public AppPaths(string? root = null) => RootDirectory = RootedFiles.ResolveRoot(Path.GetFullPath(root ?? DefaultRoot()));

    private static string DefaultRoot()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(folder))
            folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder))
            throw new InvalidOperationException("No absolute application data directory is available; configure a portable root.");
        return Path.Combine(folder, "ExileSight");
    }

    public string RootDirectory { get; }
    public string LogsDirectory => Path.Combine(RootDirectory, "logs");
    public string GetGameDirectory(string game) => Path.Combine(RootDirectory, game switch
    {
        "poe1" => "poe1",
        "poe2" => "poe2",
        _ => throw new ArgumentException("Unknown game.", nameof(game))
    });
    public string GetSettingsDirectory(string game) => GetGameDirectory(game);
}
