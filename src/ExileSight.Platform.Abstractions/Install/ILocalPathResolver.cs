namespace ExileSight.Platform.Abstractions.Install;

/// <summary>Read-only filesystem metadata; null final path means resolution failed.</summary>
public interface ILocalPathResolver
{
    bool IsRemoteDrive(string path);
    string? FinalPath(string path);
}
