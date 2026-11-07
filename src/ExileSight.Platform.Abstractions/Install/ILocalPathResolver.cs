namespace ExileSight.Platform.Abstractions.Install;

/// <summary>Read-only component checks without following unclassified links. Missing local descendants
/// return their local path; null means remote, unsafe, or inaccessible.</summary>
public interface ILocalPathResolver
{
    bool IsRemoteDrive(string path);
    string? FinalPath(string path);
}
