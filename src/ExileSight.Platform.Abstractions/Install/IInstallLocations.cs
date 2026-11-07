namespace ExileSight.Platform.Abstractions.Install;

public interface IInstallLocations
{
    IEnumerable<string> StandaloneLocations();
    IEnumerable<string> SteamLocations();
    IEnumerable<string> DefaultLocations();
    string? ReadLibraryFolders(string steamRoot);
}
